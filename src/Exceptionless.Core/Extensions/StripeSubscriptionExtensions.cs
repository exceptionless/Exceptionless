using Exceptionless.Core.Models;
using Stripe;

namespace Exceptionless.Core.Extensions;

internal static class StripeSubscriptionExtensions
{
    internal static BillingStatus? GetBillingStatus(this Subscription subscription)
        => subscription.Status switch
        {
            "active" => BillingStatus.Active,
            "canceled" => BillingStatus.Canceled,
            "incomplete" => BillingStatus.Unpaid,
            "incomplete_expired" => BillingStatus.Canceled,
            "past_due" => BillingStatus.PastDue,
            "paused" => BillingStatus.Unpaid,
            "trialing" => BillingStatus.Trialing,
            "unpaid" => BillingStatus.Unpaid,
            _ => null
        };

    internal static bool IsEnded(this Subscription subscription)
        => subscription.EndedAt.HasValue || subscription.CanceledAt.HasValue ||
            String.Equals(subscription.Status, "canceled", StringComparison.Ordinal) ||
            String.Equals(subscription.Status, "incomplete_expired", StringComparison.Ordinal);
}
