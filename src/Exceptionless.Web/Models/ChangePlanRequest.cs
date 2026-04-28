using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public record ChangePlanRequest
{
    [Required]
    public string PlanId { get; set; } = null!;

    public string? StripeToken { get; set; }

    public string? Last4 { get; set; }

    public string? CouponId { get; set; }
}
