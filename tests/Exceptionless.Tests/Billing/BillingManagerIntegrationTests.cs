using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Billing;

public sealed class BillingManagerIntegrationTests : IntegrationTestsBase
{
    public BillingManagerIntegrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    [Fact]
    public async Task TryClaimTrialAsync_ConcurrentMailboxAliases_OnlyOneAcquiresClaim()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var repository = GetService<ITrialClaimRepository>();
        var first = new User { EmailAddress = "Some.One+first@gmail.com" };
        var alias = new User { EmailAddress = "s.o.m.e.o.n.e+second@googlemail.com" };
        var options = GetService<AppOptions>();
        string claimId = TrialEmailAddress.GetClaimId(first.EmailAddress, options.BillingOptions.TrialClaimSecret!);
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
        string claimId = TrialEmailAddress.GetClaimId(original.EmailAddress, options.BillingOptions.TrialClaimSecret!);
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
    public async Task RemoveExpiredTrialClaimsAsync_MixedClaimAges_RemovesOnlyExpiredClaims()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var repository = GetService<ITrialClaimRepository>();
        var options = GetService<AppOptions>();
        string secret = options.BillingOptions.TrialClaimSecret!;
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
        Assert.False(await repository.ExistsAsync(TrialEmailAddress.GetClaimId(expired.EmailAddress, secret)));
        Assert.True(await repository.ExistsAsync(TrialEmailAddress.GetClaimId(active.EmailAddress, secret)));
    }
}
