using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
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
}
