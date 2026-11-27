namespace Exceptionless.Core.Models.WorkItems;

public record ResetProjectDataWorkItem : IBackgroundTaskWorkItem
{
    public required string OrganizationId { get; init; }
    public required string ProjectId { get; init; }
    public string? TaskId { get; init; }
}
