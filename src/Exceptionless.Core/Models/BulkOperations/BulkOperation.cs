using System.Text.Json.Serialization;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models.BulkOperations;

/// <summary>
/// A long-running action applied to every item that matches a selection. Stored in the cache with a limited
/// lifetime and indexed per organization; it is intentionally not persisted in Elasticsearch.
/// </summary>
public class BulkOperation : IIdentity, IOwnedByOrganization
{
    public string Id { get; set; } = null!;
    public string OrganizationId { get; set; } = null!;
    public string? ProjectId { get; set; }

    /// <summary>The handler key, e.g. <c>events.delete</c>.</summary>
    public string Type { get; set; } = null!;

    public string Description { get; set; } = null!;
    public BulkOperationSelection Selection { get; set; } = null!;
    public Dictionary<string, string> Parameters { get; set; } = new();
    public BulkOperationStatus Status { get; set; } = BulkOperationStatus.Queued;

    public long Total { get; set; }
    public long Processed { get; set; }
    public long Succeeded { get; set; }
    public long Failed { get; set; }

    public string CreatedByUserId { get; set; } = null!;
    public DateTime CreatedUtc { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public string? Message { get; set; }

    /// <summary>Consecutive batches that failed with a temporary error. Reset by the next successful batch.</summary>
    public int RetryCount { get; set; }

    /// <summary>Opaque position of the last batch that finished; the job resumes after it.</summary>
    public string? Cursor { get; set; }

    [JsonIgnore]
    public bool IsActive => Status is BulkOperationStatus.Queued or BulkOperationStatus.Running;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BulkOperationStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>The kind of item an operation targets. Each entity has one selection resolver.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BulkOperationEntity
{
    Events
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BulkOperationSelectionKind
{
    Ids,
    Filter
}

/// <summary>
/// Either an explicit list of ids or a filter. A filter selection always carries the absolute UTC window that was
/// resolved when the operation was created, so items that arrive afterwards are never included.
/// </summary>
public class BulkOperationSelection
{
    public BulkOperationSelectionKind Kind { get; set; }
    public List<string>? Ids { get; set; }
    public string? Filter { get; set; }

    /// <summary>The relative time expression the window was resolved from. Informational only.</summary>
    public string? Time { get; set; }

    public DateTime? UtcStart { get; set; }
    public DateTime? UtcEnd { get; set; }

    /// <summary>Items stored after this instant (UTC) are never part of a filter selection.</summary>
    public DateTime? UtcCreatedBefore { get; set; }
}
