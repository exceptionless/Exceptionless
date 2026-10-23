using Foundatio.Queues;

namespace Exceptionless.Core.Models.WorkItems;

public record OrganizationBudgetAlertWorkItem : IHaveUniqueIdentifier
{
    public required string OrganizationId { get; init; }
    public required int Threshold { get; init; }
    public required int ThresholdEventCount { get; init; }
    public required int CurrentEventCount { get; init; }
    public required int EventLimit { get; init; }
    public int UsagePeriod { get; init; }

    public string UniqueIdentifier => $"BudgetAlert:{UsagePeriod}:{OrganizationId}:{Threshold}";
    public string GetUniqueIdentifier(int fallbackUsagePeriod) => $"BudgetAlert:{(UsagePeriod > 0 ? UsagePeriod : fallbackUsagePeriod)}:{OrganizationId}:{Threshold}";
}
