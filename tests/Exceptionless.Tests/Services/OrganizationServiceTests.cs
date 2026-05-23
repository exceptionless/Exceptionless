using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Microsoft.Extensions.DependencyInjection;
using Stripe;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class OrganizationServiceTests : TestWithServices
{
    public OrganizationServiceTests(ITestOutputHelper output) : base(output) { }

    protected override void RegisterServices(IServiceCollection services, AppOptions options)
    {
        base.RegisterServices(services, options);
        services.ReplaceSingleton<IStripeBillingClient, FakeStripeBillingClient>();
    }

    [Fact]
    public async Task CancelSubscriptionsAsync_WithoutStripeCustomer_DoesNotCallStripe()
    {
        // Arrange
        var stripeClient = Assert.IsType<FakeStripeBillingClient>(GetService<IStripeBillingClient>());
        var service = GetService<OrganizationService>();

        // Act
        await service.CancelSubscriptionsAsync(new Organization { Id = "000000000000000000000001", Name = "Test" });

        // Assert
        Assert.Null(stripeClient.LastSubscriptionListOptions);
        Assert.Empty(stripeClient.CanceledSubscriptions);
    }

    [Fact]
    public async Task CancelSubscriptionsAsync_ActiveSubscriptions_CancelsOnlyActiveSubscriptions()
    {
        // Arrange
        var stripeClient = Assert.IsType<FakeStripeBillingClient>(GetService<IStripeBillingClient>());
        stripeClient.Subscriptions.Add(new Subscription { Id = "sub_active" });
        stripeClient.Subscriptions.Add(new Subscription { Id = "sub_canceled", CanceledAt = DateTime.UtcNow });
        var service = GetService<OrganizationService>();

        // Act
        await service.CancelSubscriptionsAsync(new Organization
        {
            Id = "000000000000000000000001",
            Name = "Test",
            StripeCustomerId = "cus_test"
        });

        // Assert
        Assert.Equal("cus_test", stripeClient.LastSubscriptionListOptions?.Customer);
        Assert.Equal("sub_active", Assert.Single(stripeClient.CanceledSubscriptions).SubscriptionId);
    }
}
