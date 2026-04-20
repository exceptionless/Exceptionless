using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public class ChangePlanRequest
{
    [Required]
    public required string PlanId { get; set; }

    public string? StripeToken { get; set; }

    public string? Last4 { get; set; }

    public string? CouponId { get; set; }
}
