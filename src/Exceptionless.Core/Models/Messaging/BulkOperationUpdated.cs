using Exceptionless.Core.Models.BulkOperations;

namespace Exceptionless.Core.Messaging.Models;

/// <summary>
/// Published after every batch and every status change of a bulk operation. Intentionally independent of the
/// transport that delivers it to clients, and free of the selection and parameters.
/// </summary>
public record BulkOperationUpdated
{
    public required string Id { get; set; }
    public required string OrganizationId { get; set; }
    public string? ProjectId { get; set; }
    public required string Type { get; set; }
    public required string Description { get; set; }
    public BulkOperationStatus Status { get; set; }
    public long Total { get; set; }
    public long Processed { get; set; }
    public long Succeeded { get; set; }
    public long Failed { get; set; }
    public string? Message { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
