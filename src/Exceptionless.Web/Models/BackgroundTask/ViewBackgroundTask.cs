using Exceptionless.Core.Attributes;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;

namespace Exceptionless.Web.Models;

public record ViewBackgroundTask
{
    [ObjectId]
    public string Id { get; set; } = null!;

    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    [ObjectId]
    public string? ProjectId { get; set; }

    /// <summary>What the task does, e.g. <c>events.delete</c>, <c>projects.reset-data</c> or <c>projects.sample-data</c>.</summary>
    public string Type { get; set; } = null!;

    public string Description { get; set; } = null!;
    public BackgroundTaskStatus Status { get; set; }

    /// <summary>Percent complete, 0 to 100.</summary>
    public int Progress { get; set; }

    /// <summary>The item counts. All zero for tasks that only report a percent.</summary>
    public long Total { get; set; }
    public long Processed { get; set; }
    public long Succeeded { get; set; }
    public long Failed { get; set; }

    /// <summary>Whether the task can be stopped with the cancel endpoint.</summary>
    public bool CanCancel { get; set; }

    /// <summary>What a bulk action applies to. Only set for bulk actions.</summary>
    public ViewBulkOperationSelection? Selection { get; set; }

    [ObjectId]
    public string CreatedByUserId { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string? Message { get; set; }
}

public record ViewBulkOperationSelection
{
    public BulkOperationSelectionKind Kind { get; set; }

    /// <summary>The number of ids when the selection is an id list.</summary>
    public int? IdCount { get; set; }

    public string? Filter { get; set; }
    public string? Time { get; set; }

    /// <summary>The absolute start of the resolved time window.</summary>
    public DateTime? UtcStart { get; set; }

    /// <summary>The absolute end of the resolved time window, never later than when the task was created.</summary>
    public DateTime? UtcEnd { get; set; }
}
