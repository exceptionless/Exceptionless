using System.Text.Json.Serialization;
using Exceptionless.Core.Models.BulkOperations;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models.BackgroundTasks;

/// <summary>
/// Long-running work that a user can follow in the progress panel: a bulk action, a project data reset, sample data
/// generation. Stored in the cache with a limited lifetime and indexed per organization; it is intentionally not
/// persisted in Elasticsearch. Everything a bulk batch changes (counts and cursor) lives in this one record so a
/// batch is saved atomically.
/// </summary>
public class BackgroundTask : IIdentity, IOwnedByOrganization, IHaveDates
{
    public string Id { get; set; } = null!;
    public string OrganizationId { get; set; } = null!;
    public string? ProjectId { get; set; }

    /// <summary>What the task does, e.g. <c>events.delete</c> or <c>projects.reset-data</c>.</summary>
    public string Type { get; set; } = null!;

    public string Description { get; set; } = null!;
    public BackgroundTaskStatus Status { get; set; } = BackgroundTaskStatus.Queued;

    /// <summary>Percent complete, 0 to 100.</summary>
    public int Progress { get; set; }

    /// <summary>Item counts. All zero for tasks that only report a percent.</summary>
    public long Total { get; set; }
    public long Processed { get; set; }
    public long Succeeded { get; set; }
    public long Failed { get; set; }

    public string? Message { get; set; }

    /// <summary>Whether the user can stop the task. Tasks that cannot be stopped midway are not cancellable.</summary>
    public bool CanCancel { get; set; }

    public string CreatedByUserId { get; set; } = null!;
    public DateTime CreatedUtc { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    /// <summary>Only set for bulk actions: what they apply to and where they resume.</summary>
    public BulkOperationDetails? Bulk { get; set; }

    [JsonIgnore]
    public bool IsActive => Status is BackgroundTaskStatus.Queued or BackgroundTaskStatus.Running;

    /// <summary>Marks the task as running. The start time is only recorded the first time.</summary>
    public void Start(DateTime utcNow)
    {
        Status = BackgroundTaskStatus.Running;
        StartedUtc ??= utcNow;
    }

    public void Finish(BackgroundTaskStatus status, string? message, DateTime utcNow)
    {
        Status = status;
        Message = message;
        CompletedUtc = utcNow;
        if (status is BackgroundTaskStatus.Completed)
            Progress = 100;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackgroundTaskStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}
