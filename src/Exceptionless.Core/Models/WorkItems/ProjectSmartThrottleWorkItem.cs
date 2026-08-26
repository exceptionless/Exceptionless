using Foundatio.Queues;

namespace Exceptionless.Core.Models.WorkItems;

public record ProjectSmartThrottleWorkItem : IHaveUniqueIdentifier
{
    public required string OrganizationId { get; init; }
    public required string ProjectId { get; init; }
    public required double SampleRate { get; init; }
    public required int CurrentEventCount { get; init; }
    public required int EventLimit { get; init; }
    public int UsagePeriod { get; init; }

    public string UniqueIdentifier => $"SmartThrottle:{UsagePeriod}:{OrganizationId}:{ProjectId}";
    public string GetUniqueIdentifier(int fallbackUsagePeriod) => $"SmartThrottle:{(UsagePeriod > 0 ? UsagePeriod : fallbackUsagePeriod)}:{OrganizationId}:{ProjectId}";
}
