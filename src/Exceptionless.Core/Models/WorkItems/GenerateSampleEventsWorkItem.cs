namespace Exceptionless.Core.Models.WorkItems;

public record GenerateSampleEventsWorkItem
{
    public string? OrganizationId { get; init; }
    public string? ProjectId { get; init; }
    public int EventCount { get; init; } = 250;
    public int DaysBack { get; init; } = 7;
}
