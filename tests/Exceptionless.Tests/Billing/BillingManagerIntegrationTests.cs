using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Exceptions;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Billing;

public sealed class BillingManagerIntegrationTests : IntegrationTestsBase
{
    public BillingManagerIntegrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TryClaimTrialAsync_ClaimDisappearsAfterConflict_RecreatesOnlyWhenNoCompetitorClaimsIt(bool competingClaim)
    {
        // Arrange
        var repository = GetService<ITrialClaimRepository>();
        var billing = GetService<BillingManager>();
        var user = new User { EmailAddress = "cleanup-race@example.test" };
        int attempts = 0;
        using var handler = repository.DocumentsAdding.AddHandler(async (_, args) =>
        {
            int attempt = Interlocked.Increment(ref attempts);
            if (attempt is 1)
                throw new DuplicateDocumentException("The expired claim existed when create started");
            if (attempt is 2 && competingClaim)
                await repository.AddAsync(new TrialClaim { Id = args.Documents.Single().Id, CreatedUtc = TimeProvider.GetUtcNow().UtcDateTime });
        });

        // Act
        bool acquired = await billing.TryClaimTrialAsync(user);
        bool eligible = await billing.CanStartTrialAsync(user);

        // Assert
        Assert.Equal(!competingClaim, acquired);
        Assert.False(eligible);
    }

    [Theory]
    [InlineData(2, 0, 1, "user")]
    [InlineData(1, 1, 1, "invitation")]
    [InlineData(1, 0, 2, "project")]
    public async Task CanDownGradeAsync_FinitePlanCountsMembersInvitesAndProjects_RejectsExcess(int memberCount, int inviteCount, int projectCount, string expectedMessage)
    {
        // Arrange
        var billing = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();
        var organizations = GetService<IOrganizationRepository>();
        var userRepository = GetService<IUserRepository>();
        var projectRepository = GetService<IProjectRepository>();
        var userData = GetService<UserData>();
        var projectData = GetService<ProjectData>();
        var organization = new Organization { Name = "Finite custom plan" };
        billing.ApplyTrialPlan(organization);
        for (int index = 0; index < inviteCount; index++)
            organization.Invites.Add(new Invite { EmailAddress = $"invite-{index}@example.test", Token = StringExtensions.GetNewToken(), DateAdded = TimeProvider.GetUtcNow().UtcDateTime });
        await organizations.AddAsync(organization);
        var users = Enumerable.Range(0, memberCount).Select(index =>
        {
            var user = userData.GenerateUser(generateId: true, organizationId: organization.Id);
            user.MarkEmailAddressVerified();
            return user;
        });
        await userRepository.AddAsync(users, command => command.ImmediateConsistency());
        await projectRepository.AddAsync(projectData.GenerateProjects(projectCount, generateId: true, organizationId: organization.Id), command => command.ImmediateConsistency());
        var customPlan = plans.SmallPlan with { Id = "CUSTOM_FINITE", MaxUsers = 1, MaxProjects = 1 };

        // Act
        var finite = await billing.CanDownGradeAsync(organization, customPlan, null);
        var unlimited = await billing.CanDownGradeAsync(organization, plans.SmallPlan, null);

        // Assert
        Assert.False(finite.Success);
        Assert.Contains(expectedMessage, finite.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(unlimited.Success, unlimited.Message);
    }

    [Fact]
    public async Task TryClaimTrialAsync_ConcurrentMailboxAliases_OnlyOneAcquiresClaim()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var repository = GetService<ITrialClaimRepository>();
        var first = new User { EmailAddress = "Some.One+first@gmail.com" };
        var alias = new User { EmailAddress = "s.o.m.e.o.n.e+second@googlemail.com" };
        string claimId = TrialEmailAddress.GetClaimId(first.EmailAddress);
        bool existedBefore = await repository.ExistsAsync(claimId);

        // Act: bypass the mailbox lock to exercise the durable uniqueness boundary directly.
        var acquired = await Task.WhenAll(billingManager.TryClaimTrialAsync(first), billingManager.TryClaimTrialAsync(alias));
        bool existsAfter = await repository.ExistsAsync(claimId);
        bool acquiredAgain = await billingManager.TryClaimTrialAsync(first);

        // Assert
        Assert.False(existedBefore);
        Assert.Single(acquired, claimed => claimed);
        Assert.True(existsAfter);
        Assert.False(acquiredAgain);
    }

    [Fact]
    public async Task CanStartTrialAsync_MailboxClaimedWithinRetention_ReturnsFalse()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var options = GetService<AppOptions>();
        var original = new User { Id = ObjectId.GenerateNewId().ToString(), EmailAddress = "within.retention@example.com" };
        var returning = new User { Id = ObjectId.GenerateNewId().ToString(), EmailAddress = "within.retention+again@example.com" };
        await billingManager.TryClaimTrialAsync(original);
        TimeProvider.Advance(TimeSpan.FromDays(options.BillingOptions.TrialClaimRetentionDays - 1));

        // Act
        bool canStartTrial = await billingManager.CanStartTrialAsync(returning);

        // Assert
        Assert.False(canStartTrial);
    }

    [Fact]
    public async Task TryClaimTrialAsync_ClaimOlderThanRetention_RenewsClaim()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var repository = GetService<ITrialClaimRepository>();
        var options = GetService<AppOptions>();
        var original = new User { Id = ObjectId.GenerateNewId().ToString(), EmailAddress = "past.retention@example.com" };
        var returning = new User { Id = ObjectId.GenerateNewId().ToString(), EmailAddress = "past.retention+again@example.com" };
        string claimId = TrialEmailAddress.GetClaimId(original.EmailAddress);
        await billingManager.TryClaimTrialAsync(original);
        TimeProvider.Advance(TimeSpan.FromDays(options.BillingOptions.TrialClaimRetentionDays + 1));
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;

        // Act
        bool canStartTrial = await billingManager.CanStartTrialAsync(returning);
        bool claimed = await billingManager.TryClaimTrialAsync(returning);
        bool claimedAgain = await billingManager.TryClaimTrialAsync(returning);
        var claim = await repository.GetByIdAsync(claimId);

        // Assert
        Assert.True(canStartTrial);
        Assert.True(claimed);
        Assert.False(claimedAgain);
        Assert.NotNull(claim);
        Assert.Equal(utcNow, claim.CreatedUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CanStartTrialAsync_PreviousTrialOlderThanRetention_ReturnsTrue()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var options = GetService<AppOptions>();
        var user = new User
        {
            Id = ObjectId.GenerateNewId().ToString(),
            EmailAddress = "previous.trial@example.com",
            TrialStartedUtc = TimeProvider.GetUtcNow().UtcDateTime
        };
        bool canStartTrialBefore = await billingManager.CanStartTrialAsync(user);
        TimeProvider.Advance(TimeSpan.FromDays(options.BillingOptions.TrialClaimRetentionDays + 1));

        // Act
        bool canStartTrialAfter = await billingManager.CanStartTrialAsync(user);

        // Assert
        Assert.False(canStartTrialBefore);
        Assert.True(canStartTrialAfter);
    }

    [Fact]
    public async Task TryClaimTrialAsync_ConcurrentExpiredMailboxAliases_OnlyOneRenewsClaim()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var repository = GetService<ITrialClaimRepository>();
        var options = GetService<AppOptions>();
        var first = new User { EmailAddress = "renewal.race@gmail.com" };
        var alias = new User { EmailAddress = "renewalrace+again@googlemail.com" };
        await billingManager.TryClaimTrialAsync(first);
        TimeProvider.Advance(TimeSpan.FromDays(options.BillingOptions.TrialClaimRetentionDays + 1));
        var bothRenewing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int arrivals = 0;
        using var handler = repository.DocumentsChanging.AddHandler(async (_, args) =>
        {
            if (args.ChangeType is not ChangeType.Saved)
                return;

            if (Interlocked.Increment(ref arrivals) is 2)
                bothRenewing.SetResult();
            await bothRenewing.Task.WaitAsync(TimeSpan.FromSeconds(10), TestCancellationToken);
        });

        // Act: bypass mailbox locks to verify storage still admits only one renewal.
        var acquired = await Task.WhenAll(billingManager.TryClaimTrialAsync(first), billingManager.TryClaimTrialAsync(alias));

        // Assert
        Assert.Single(acquired, claimed => claimed);
    }

    [Fact]
    public async Task RemoveExpiredTrialClaimsAsync_MixedClaimAges_RemovesOnlyExpiredClaims()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var repository = GetService<ITrialClaimRepository>();
        var options = GetService<AppOptions>();
        var expired = new User { EmailAddress = "expired.claim@example.com" };
        var active = new User { EmailAddress = "active.claim@example.com" };
        await billingManager.TryClaimTrialAsync(expired);
        TimeProvider.Advance(TimeSpan.FromDays(options.BillingOptions.TrialClaimRetentionDays - 10));
        await billingManager.TryClaimTrialAsync(active);
        TimeProvider.Advance(TimeSpan.FromDays(11));
        await RefreshDataAsync();

        // Act
        long removed = await billingManager.RemoveExpiredTrialClaimsAsync();

        // Assert
        Assert.Equal(1, removed);
        Assert.False(await repository.ExistsAsync(TrialEmailAddress.GetClaimId(expired.EmailAddress)));
        Assert.True(await repository.ExistsAsync(TrialEmailAddress.GetClaimId(active.EmailAddress)));
    }
}
