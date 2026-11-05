namespace Exceptionless.Core.Models.WorkItems;

public record BulkOperationWorkItem : IBackgroundTaskWorkItem
{
    public required string TaskId { get; init; }

    /// <summary>
    /// The organization, type and description are carried so a final state can still be published if the task
    /// record is gone when the job runs.
    /// </summary>
    public required string OrganizationId { get; init; }

    public string? Type { get; init; }
    public string? Description { get; init; }
}
