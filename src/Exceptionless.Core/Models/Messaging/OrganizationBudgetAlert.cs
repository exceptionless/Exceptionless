namespace Exceptionless.Core.Messaging.Models;

public record OrganizationBudgetAlert
{
    public required string OrganizationId { get; init; }
    public required int Threshold { get; init; }
    public required int ThresholdEventCount { get; init; }
    public required int CurrentEventCount { get; init; }
    public required int EventLimit { get; init; }
    public int UsagePeriod { get; init; }
}
