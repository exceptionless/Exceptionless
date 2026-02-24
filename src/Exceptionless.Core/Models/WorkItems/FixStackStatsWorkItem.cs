namespace Exceptionless.Core.Models.WorkItems;

public record FixStackStatsWorkItem
{
    public DateTime UtcStart { get; init; }

    public DateTime? UtcEnd { get; init; }

    /// <summary>
    /// When set, only stacks belonging to this organization are repaired.
    /// When null, all organizations with events in the time window are processed.
    /// </summary>
    public string? Organization { get; init; }
}
