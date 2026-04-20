namespace Exceptionless.Web.Models;

/// <summary>
/// Request body for the change-plan endpoint.
/// Accepted as JSON body from the Svelte client; query params remain supported for the legacy Angular client.
/// Property names use snake_case to match the Exceptionless API JSON convention.
/// </summary>
public class ChangePlanRequest
{
    /// <summary>The plan ID to switch to.</summary>
    public string? PlanId { get; set; }

    /// <summary>Stripe PaymentMethod ID (pm_...) from the modern Svelte UI, or legacy tok_... token.</summary>
    public string? StripeToken { get; set; }

    /// <summary>Last 4 digits of the card for display purposes.</summary>
    public string? Last4 { get; set; }

    /// <summary>Optional coupon/promotion code to apply.</summary>
    public string? CouponId { get; set; }
}
