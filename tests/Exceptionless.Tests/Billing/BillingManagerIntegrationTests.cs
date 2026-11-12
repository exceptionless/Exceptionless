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
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _billingPlans;
    private readonly AppOptions _options;
    private readonly ITrialClaimRepository _trialClaimRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly UserData _userData;
    private readonly ProjectData _projectData;

    public BillingManagerIntegrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _billingManager = GetService<BillingManager>();
        _billingPlans = GetService<BillingPlans>();
        _options = GetService<AppOptions>();
        _trialClaimRepository = GetService<ITrialClaimRepository>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _userRepository = GetService<IUserRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _userData = GetService<UserData>();
        _projectData = GetService<ProjectData>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TryClaimTrialAsync_ClaimDisappearsAfterConflict_RecreatesOnlyWhenNoCompetitorClaimsIt(bool competingClaim)
    {
        // Arrange
        var user = new User { EmailAddress = "cleanup-race@example.test" };
        int attempts = 0;
        using var handler = _trialClaimRepository.DocumentsAdding.AddHandler(async (_, args) =>
        {
            int attempt = Interlocked.Increment(ref attempts);
            if (attempt is 1)
                throw new DuplicateDocumentException("The expired claim existed when create started");
            if (attempt is 2 && competingClaim)
                await _trialClaimRepository.AddAsync(new TrialClaim { Id = args.Documents.Single().Id, CreatedUtc = TimeProvider.GetUtcNow().UtcDateTime });
        });

        // Act
        bool acquired = await _billingManager.TryClaimTrialAsync(user);
        bool eligible = await _billingManager.CanStartTrialAsync(user);

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
        var organization = new Organization { Name = "Finite custom plan" };
        _billingManager.ApplyTrialPlan(organization);
        for (int index = 0; index < inviteCount; index++)
            organization.Invites.Add(new Invite { EmailAddress = $"invite-{index}@example.test", Token = StringExtensions.GetNewToken(), DateAdded = TimeProvider.GetUtcNow().UtcDateTime });
        await _organizationRepository.AddAsync(organization);
        var users = Enumerable.Range(0, memberCount).Select(index =>
        {
            var user = _userData.GenerateUser(generateId: true, organizationId: organization.Id);
            user.MarkEmailAddressVerified();
            return user;
        });
        await _userRepository.AddAsync(users, command => command.ImmediateConsistency());
        await _projectRepository.AddAsync(_projectData.GenerateProjects(projectCount, generateId: true, organizationId: organization.Id), command => command.ImmediateConsistency());
        var customPlan = _billingPlans.SmallPlan with { Id = "CUSTOM_FINITE", MaxUsers = 1, MaxProjects = 1 };

        // Act
        var finite = await _billingManager.CanDownGradeAsync(organization, customPlan, null);
        var unlimited = await _billingManager.CanDownGradeAsync(organization, _billingPlans.SmallPlan, null);

        // Assert
        Assert.False(finite.Success);
        Assert.Contains(expectedMessage, finite.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(unlimited.Success, unlimited.Message);
    }

    [Fact]
    public async Task TryClaimTrialAsync_ConcurrentMailboxAliases_OnlyOneAcquiresClaim()
    {
        // Arrange
        var first = new User { EmailAddress = "Some.One+first@gmail.com" };
        var alias = new User { EmailAddress = "s.o.m.e.o.n.e+second@googlemail.com" };
        string claimId = TrialEmailAddress.GetClaimId(first.EmailAddress);
        bool existedBefore = await _trialClaimRepository.ExistsAsync(claimId);

        // Act: bypass the mailbox lock to exercise the durable uniqueness boundary directly.
        var acquired = await Task.WhenAll(_billingManager.TryClaimTrialAsync(first), _billingManager.TryClaimTrialAsync(alias));
        bool existsAfter = await _trialClaimRepository.ExistsAsync(claimId);
        bool acquiredAgain = await _billingManager.TryClaimTrialAsync(first);

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
        var original = new User { Id = ObjectId.GenerateNewId().ToString(), EmailAddress = "within.retention@example.com" };
        var returning = new User { Id = ObjectId.GenerateNewId().ToString(), EmailAddress = "within.retention+again@example.com" };
        await _billingManager.TryClaimTrialAsync(original);
        TimeProvider.Advance(TimeSpan.FromDays(_options.BillingOptions.TrialClaimRetentionDays - 1));

        // Act
        bool canStartTrial = await _billingManager.CanStartTrialAsync(returning);

        // Assert
        Assert.False(canStartTrial);
    }

    [Fact]
    public async Task TryClaimTrialAsync_ClaimOlderThanRetention_RenewsClaim()
    {
        // Arrange
        var original = new User { Id = ObjectId.GenerateNewId().ToString(), EmailAddress = "past.retention@example.com" };
        var returning = new User { Id = ObjectId.GenerateNewId().ToString(), EmailAddress = "past.retention+again@example.com" };
        string claimId = TrialEmailAddress.GetClaimId(original.EmailAddress);
        await _billingManager.TryClaimTrialAsync(original);
        TimeProvider.Advance(TimeSpan.FromDays(_options.BillingOptions.TrialClaimRetentionDays + 1));
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;

        // Act
        bool canStartTrial = await _billingManager.CanStartTrialAsync(returning);
        bool claimed = await _billingManager.TryClaimTrialAsync(returning);
        bool claimedAgain = await _billingManager.TryClaimTrialAsync(returning);
        var claim = await _trialClaimRepository.GetByIdAsync(claimId);

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
        var user = new User
        {
            Id = ObjectId.GenerateNewId().ToString(),
            EmailAddress = "previous.trial@example.com",
            TrialStartedUtc = TimeProvider.GetUtcNow().UtcDateTime
        };
        bool canStartTrialBefore = await _billingManager.CanStartTrialAsync(user);
        TimeProvider.Advance(TimeSpan.FromDays(_options.BillingOptions.TrialClaimRetentionDays + 1));

        // Act
        bool canStartTrialAfter = await _billingManager.CanStartTrialAsync(user);

        // Assert
        Assert.False(canStartTrialBefore);
        Assert.True(canStartTrialAfter);
    }

    [Fact]
    public async Task TryClaimTrialAsync_ConcurrentExpiredMailboxAliases_OnlyOneRenewsClaim()
    {
        // Arrange
        var first = new User { EmailAddress = "renewal.race@gmail.com" };
        var alias = new User { EmailAddress = "renewalrace+again@googlemail.com" };
        await _billingManager.TryClaimTrialAsync(first);
        TimeProvider.Advance(TimeSpan.FromDays(_options.BillingOptions.TrialClaimRetentionDays + 1));
        var bothRenewing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int arrivals = 0;
        using var handler = _trialClaimRepository.DocumentsChanging.AddHandler(async (_, args) =>
        {
            if (args.ChangeType is not ChangeType.Saved)
                return;

            if (Interlocked.Increment(ref arrivals) is 2)
                bothRenewing.SetResult();
            await bothRenewing.Task.WaitAsync(TimeSpan.FromSeconds(10), TestCancellationToken);
        });

        // Act: bypass mailbox locks to verify storage still admits only one renewal.
        var acquired = await Task.WhenAll(_billingManager.TryClaimTrialAsync(first), _billingManager.TryClaimTrialAsync(alias));

        // Assert
        Assert.Single(acquired, claimed => claimed);
    }

    [Fact]
    public async Task RemoveExpiredTrialClaimsAsync_MixedClaimAges_RemovesOnlyExpiredClaims()
    {
        // Arrange
        var expired = new User { EmailAddress = "expired.claim@example.com" };
        var active = new User { EmailAddress = "active.claim@example.com" };
        await _billingManager.TryClaimTrialAsync(expired);
        TimeProvider.Advance(TimeSpan.FromDays(_options.BillingOptions.TrialClaimRetentionDays - 10));
        await _billingManager.TryClaimTrialAsync(active);
        TimeProvider.Advance(TimeSpan.FromDays(11));
        await RefreshDataAsync();

        // Act
        long removed = await _billingManager.RemoveExpiredTrialClaimsAsync();

        // Assert
        Assert.Equal(1, removed);
        Assert.False(await _trialClaimRepository.ExistsAsync(TrialEmailAddress.GetClaimId(expired.EmailAddress)));
        Assert.True(await _trialClaimRepository.ExistsAsync(TrialEmailAddress.GetClaimId(active.EmailAddress)));
    }
}
