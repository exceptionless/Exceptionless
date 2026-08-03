using Exceptionless.Core.Extensions;
using Stripe;

namespace Exceptionless.Core.Billing;

public static class StripeBillingClientExtensions
{
    public static async Task<IReadOnlyCollection<Subscription>> GetLiveSubscriptionsAsync(
        this IStripeBillingClient stripeBillingClient,
        string customerId)
    {
        var subscriptions = await stripeBillingClient.ListSubscriptionsAsync(new SubscriptionListOptions
        {
            Customer = customerId,
            Limit = 100
        });

        return subscriptions.Where(IsLiveSubscription).ToList();
    }

    public static async Task FinalizePendingCancellationCreditsAsync(
        this IStripeBillingClient stripeBillingClient,
        string customerId)
    {
        var draftInvoices = await stripeBillingClient.ListInvoicesAsync(new InvoiceListOptions
        {
            Customer = customerId,
            Status = "draft",
            Limit = 100
        });

        foreach (var invoice in draftInvoices.Where(IsPendingSubscriptionCredit))
            await FinalizeInvoiceIfDraftAsync(stripeBillingClient, customerId, invoice);
    }

    public static Subscription? SelectPrimarySubscription(
        this IReadOnlyCollection<Subscription> subscriptions,
        string? preferredSubscriptionId,
        string currentPlanId,
        string targetPlanId)
    {
        if (subscriptions.Count <= 1)
            return subscriptions.SingleOrDefault();

        var healthySubscriptions = subscriptions.Where(IsHealthySubscription).ToList();
        var candidates = (healthySubscriptions.Count > 0 ? healthySubscriptions : subscriptions)
            .OrderByDescending(subscription => GetStatusPriority(subscription.Status))
            .ThenBy(subscription => subscription.Created)
            .ThenBy(subscription => subscription.Id, StringComparer.Ordinal)
            .ToList();

        return candidates.FirstOrDefault(subscription =>
                String.Equals(subscription.Id, preferredSubscriptionId, StringComparison.Ordinal))
            ?? candidates.FirstOrDefault(subscription => HasPrice(subscription, targetPlanId))
            ?? candidates.FirstOrDefault(subscription => HasPrice(subscription, currentPlanId))
            ?? candidates[0];
    }

    public static async Task CancelSubscriptionWithProrationAsync(
        this IStripeBillingClient stripeBillingClient,
        string customerId,
        Subscription subscription)
    {
        var canceledSubscription = await stripeBillingClient.CancelSubscriptionAsync(subscription.Id,
            new SubscriptionCancelOptions { Prorate = true, InvoiceNow = true });

        if (String.IsNullOrEmpty(canceledSubscription.LatestInvoiceId) ||
            String.Equals(canceledSubscription.LatestInvoiceId, subscription.LatestInvoiceId, StringComparison.Ordinal))
        {
            return;
        }

        var invoice = await stripeBillingClient.GetInvoiceAsync(canceledSubscription.LatestInvoiceId)
            ?? throw new InvalidOperationException($"Stripe invoice {canceledSubscription.LatestInvoiceId} was not found after canceling a subscription.");

        await FinalizeInvoiceIfDraftAsync(stripeBillingClient, customerId, invoice);
    }

    private static bool IsLiveSubscription(Subscription subscription)
        => !subscription.IsEnded();

    private static bool IsPendingSubscriptionCredit(Stripe.Invoice invoice)
        => invoice.Total < 0 &&
            (!String.IsNullOrEmpty(invoice.Parent?.SubscriptionDetails?.SubscriptionId) ||
                invoice.BillingReason?.StartsWith("subscription", StringComparison.Ordinal) is true);

    private static bool HasPrice(Subscription subscription, string planId)
        => subscription.Items.Data.Any(item =>
            String.Equals(item.Price?.Id, planId, StringComparison.Ordinal) ||
            String.Equals(item.Plan?.Id, planId, StringComparison.Ordinal));

    private static bool IsHealthySubscription(Subscription subscription)
        => String.Equals(subscription.Status, "active", StringComparison.Ordinal) ||
            String.Equals(subscription.Status, "trialing", StringComparison.Ordinal);

    private static int GetStatusPriority(string? status)
        => status switch
        {
            "active" => 3,
            "trialing" => 2,
            "past_due" => 1,
            _ => 0
        };

    private static async Task FinalizeInvoiceIfDraftAsync(
        IStripeBillingClient stripeBillingClient,
        string customerId,
        Stripe.Invoice invoice)
    {
        if (!String.Equals(invoice.CustomerId, customerId, StringComparison.Ordinal))
            throw new InvalidOperationException($"Stripe invoice {invoice.Id} does not belong to the expected customer.");

        if (!String.Equals(invoice.Status, "draft", StringComparison.OrdinalIgnoreCase))
            return;

        var finalizedInvoice = await stripeBillingClient.FinalizeInvoiceAsync(invoice.Id,
            new InvoiceFinalizeOptions { AutoAdvance = true },
            $"exceptionless-cancellation-invoice-{invoice.Id}");

        if (invoice.Total < 0 &&
            (!String.Equals(finalizedInvoice.Status, "paid", StringComparison.OrdinalIgnoreCase) || !finalizedInvoice.EndingBalance.HasValue))
        {
            throw new InvalidOperationException($"Stripe invoice {invoice.Id} was not fully finalized into the customer balance.");
        }
    }
}
