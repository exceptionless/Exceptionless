namespace Exceptionless.Core.Models.WorkItems;

public record FixStackStatsWorkItem
{
    public DateTime UtcStart { get; init; }

    public DateTime? UtcEnd { get; init; }
}
