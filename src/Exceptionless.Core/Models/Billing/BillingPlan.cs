using System.Diagnostics;

namespace Exceptionless.Core.Models.Billing;

[DebuggerDisplay("Id: {Id} Name: {Name} Price: {Price}")]
public record BillingPlan
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public decimal Price { get; init; }
    public int MaxProjects { get; init; }
    public int MaxUsers { get; init; }
    public int RetentionDays { get; init; }
    public int MaxEventsPerMonth { get; init; }
    public bool HasPremiumFeatures { get; init; }
    public bool IsHidden { get; init; }
}
