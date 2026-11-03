namespace Exceptionless.Core.Models.WorkItems;

public record BulkOperationWorkItem
{
    public required string OperationId { get; init; }
}
