namespace Exceptionless.Core.Models.WorkItems;

public record BulkOperationWorkItem
{
    public required string OperationId { get; init; }

    /// <summary>
    /// The organization, type and description are carried so a final state can still be published if the operation
    /// record is gone when the job runs.
    /// </summary>
    public required string OrganizationId { get; init; }

    public string? Type { get; init; }
    public string? Description { get; init; }
}
