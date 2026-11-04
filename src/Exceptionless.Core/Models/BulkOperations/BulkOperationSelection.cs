using System.Text.Json.Serialization;

namespace Exceptionless.Core.Models.BulkOperations;

/// <summary>The part of a <see cref="BackgroundTasks.BackgroundTask"/> that only bulk actions have.</summary>
public class BulkOperationDetails
{
    public BulkOperationSelection Selection { get; set; } = null!;
    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>Consecutive batches that failed with a temporary error. Reset by the next successful batch.</summary>
    public int RetryCount { get; set; }

    /// <summary>Opaque position of the last batch that finished; the job resumes after it.</summary>
    public string? Cursor { get; set; }
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
