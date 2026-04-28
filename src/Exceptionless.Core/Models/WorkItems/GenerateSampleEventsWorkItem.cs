namespace Exceptionless.Core.Models.WorkItems;

public record GenerateSampleEventsWorkItem
{
    public int EventCount { get; init; } = 100;
    public int DaysBack { get; init; } = 7;
}
