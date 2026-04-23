using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public class ChangePlanRequest
{
    [Required]
    public string PlanId { get; set; } = String.Empty;

    public string? StripeToken { get; set; }

    public string? Last4 { get; set; }

    public string? CouponId { get; set; }
}
