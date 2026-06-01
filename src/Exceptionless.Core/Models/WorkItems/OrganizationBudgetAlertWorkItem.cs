using Foundatio.Queues;

namespace Exceptionless.Core.Models.WorkItems;

public record OrganizationBudgetAlertWorkItem : IHaveUniqueIdentifier
{
    public required string OrganizationId { get; init; }
    public required int Threshold { get; init; }
    public required int ThresholdEventCount { get; init; }
    public required int CurrentEventCount { get; init; }
    public required int EventLimit { get; init; }

    public string UniqueIdentifier => $"BudgetAlert:{OrganizationId}:{Threshold}";
}
