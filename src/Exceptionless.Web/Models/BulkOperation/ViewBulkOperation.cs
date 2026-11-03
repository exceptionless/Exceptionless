using Exceptionless.Core.Attributes;
using Exceptionless.Core.Models.BulkOperations;

namespace Exceptionless.Web.Models;

public record ViewBulkOperation
{
    [ObjectId]
    public string Id { get; set; } = null!;

    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    [ObjectId]
    public string? ProjectId { get; set; }

    public string Type { get; set; } = null!;
    public string Description { get; set; } = null!;
    public BulkOperationStatus Status { get; set; }
    public ViewBulkOperationSelection Selection { get; set; } = null!;

    public long Total { get; set; }
    public long Processed { get; set; }
    public long Succeeded { get; set; }
    public long Failed { get; set; }

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

    /// <summary>The absolute end of the resolved time window, never later than when the operation was created.</summary>
    public DateTime? UtcEnd { get; set; }
}
