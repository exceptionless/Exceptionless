using Exceptionless.Core.Billing;
using Exceptionless.Tests.Utility;
using Stripe;
using Xunit;

namespace Exceptionless.Tests.Billing;

public sealed class StripeBillingClientExtensionsTests
{
    [Fact]
    public void SelectPrimarySubscription_MultipleSubscriptions_PrefersPersistedSubscription()
    {
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("sub_old", "small-yearly", "active", DateTime.UtcNow.AddDays(-1)),
            CreateSubscription("sub_persisted", "small-yearly", "active", DateTime.UtcNow)
        };

        var subscription = subscriptions.SelectPrimarySubscription("sub_persisted", "free", "small-yearly");

        Assert.NotNull(subscription);
        Assert.Equal("sub_persisted", subscription.Id);
    }

    [Fact]
    public void SelectPrimarySubscription_MultipleSubscriptions_PrefersTargetPlan()
    {
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("sub_medium", "medium-yearly", "active", DateTime.UtcNow.AddDays(-1)),
            CreateSubscription("sub_small", "small-yearly", "active", DateTime.UtcNow)
        };

        var subscription = subscriptions.SelectPrimarySubscription(null, "free", "small-yearly");

        Assert.NotNull(subscription);
        Assert.Equal("sub_small", subscription.Id);
    }

    [Fact]
    public void SelectPrimarySubscription_UnhealthyPersistedSubscription_PrefersHealthySubscription()
    {
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("sub_persisted", "small-yearly", "past_due", DateTime.UtcNow.AddDays(-1)),
            CreateSubscription("sub_active", "medium-yearly", "active", DateTime.UtcNow)
        };

        var subscription = subscriptions.SelectPrimarySubscription("sub_persisted", "small-yearly", "small-yearly");

        Assert.NotNull(subscription);
        Assert.Equal("sub_active", subscription.Id);
    }

    [Fact]
    public void SelectPrimarySubscription_EquivalentSubscriptions_PrefersOldestHealthySubscription()
    {
        var subscriptions = new List<Subscription>
        {
            CreateSubscription("sub_new", "small-yearly", "active", DateTime.UtcNow),
            CreateSubscription("sub_old", "small-yearly", "active", DateTime.UtcNow.AddDays(-1)),
            CreateSubscription("sub_past_due", "small-yearly", "past_due", DateTime.UtcNow.AddDays(-2))
        };

        var subscription = subscriptions.SelectPrimarySubscription(null, "free", "small-yearly");

        Assert.NotNull(subscription);
        Assert.Equal("sub_old", subscription.Id);
    }

    [Fact]
    public async Task CancelSubscriptionAsync_UnpaidSubscription_DoesNotIssueCredit()
    {
        var stripeBillingClient = new FakeStripeBillingClient();
        var subscription = CreateSubscription("sub_unpaid", "small-yearly", "past_due", DateTime.UtcNow);
        subscription.CustomerId = "cus_test";
        subscription.LatestInvoiceId = "in_unpaid";

        await stripeBillingClient.CancelSubscriptionAsync("cus_test", subscription);

        var cancellation = Assert.Single(stripeBillingClient.CanceledSubscriptions);
        Assert.False(cancellation.Options.Prorate);
        Assert.False(cancellation.Options.InvoiceNow);
        Assert.Null(stripeBillingClient.LastGetInvoiceId);
        Assert.Empty(stripeBillingClient.FinalizedInvoices);
    }

    [Fact]
    public async Task FinalizePendingCancellationCreditsAsync_CanceledSubscription_FinalizesCredit()
    {
        var stripeBillingClient = new FakeStripeBillingClient();
        var subscription = CreateSubscription("sub_canceled", "small-yearly", "canceled", DateTime.UtcNow);
        subscription.CustomerId = "cus_test";
        subscription.CanceledAt = DateTime.UtcNow;
        stripeBillingClient.Subscriptions.Add(subscription);
        stripeBillingClient.Invoices.Add(CreateDraftCreditInvoice("in_credit", subscription.Id));

        await stripeBillingClient.FinalizePendingCancellationCreditsAsync("cus_test");

        Assert.Equal("in_credit", Assert.Single(stripeBillingClient.FinalizedInvoices).InvoiceId);
    }

    [Fact]
    public async Task FinalizePendingCancellationCreditsAsync_LiveSubscription_DoesNotFinalizeCredit()
    {
        var stripeBillingClient = new FakeStripeBillingClient();
        var subscription = CreateSubscription("sub_active", "small-yearly", "active", DateTime.UtcNow);
        subscription.CustomerId = "cus_test";
        stripeBillingClient.Subscriptions.Add(subscription);
        stripeBillingClient.Invoices.Add(CreateDraftCreditInvoice("in_credit", subscription.Id));

        await stripeBillingClient.FinalizePendingCancellationCreditsAsync("cus_test");

        Assert.Empty(stripeBillingClient.FinalizedInvoices);
    }

    [Fact]
    public async Task GetLiveSubscriptionsAsync_PeriodEndCancellation_RemainsLive()
    {
        var stripeBillingClient = new FakeStripeBillingClient();
        var subscription = CreateSubscription("sub_scheduled", "small-yearly", "active", DateTime.UtcNow);
        subscription.CanceledAt = DateTime.UtcNow;
        subscription.CancelAtPeriodEnd = true;
        stripeBillingClient.Subscriptions.Add(subscription);

        var subscriptions = await stripeBillingClient.GetLiveSubscriptionsAsync("cus_test");

        Assert.Equal("sub_scheduled", Assert.Single(subscriptions).Id);
    }

    private static Subscription CreateSubscription(string id, string priceId, string status, DateTime createdUtc)
        => new()
        {
            Id = id,
            Status = status,
            Created = createdUtc,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Id = $"si_{id}", Price = new Price { Id = priceId } }]
            }
        };

    private static Stripe.Invoice CreateDraftCreditInvoice(string id, string subscriptionId)
        => new()
        {
            Id = id,
            CustomerId = "cus_test",
            Status = "draft",
            Total = -1000,
            Parent = new InvoiceParent
            {
                Type = "subscription_details",
                SubscriptionDetails = new InvoiceParentSubscriptionDetails { SubscriptionId = subscriptionId }
            }
        };
}
