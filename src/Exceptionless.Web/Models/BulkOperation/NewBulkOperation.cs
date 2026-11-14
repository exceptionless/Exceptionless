using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Attributes;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BulkOperations;

namespace Exceptionless.Web.Models;

public record NewBulkOperation : IOwnedByOrganization
{
    /// <summary>The action to run, e.g. <c>events.delete</c>.</summary>
    [Required]
    [MaxLength(100)]
    public string Type { get; set; } = null!;

    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    /// <summary>Optionally limits the operation to one project of the organization.</summary>
    [ObjectId]
    public string? ProjectId { get; set; }

    [Required]
    public NewBulkOperationSelection Selection { get; set; } = null!;

    /// <summary>Action specific parameters. Actions without parameters reject any value here.</summary>
    public Dictionary<string, string>? Parameters { get; set; }
}

public record NewBulkOperationSelection
{
    public BulkOperationSelectionKind Kind { get; set; }

    /// <summary>The ids to process when <see cref="Kind"/> is <c>ids</c>.</summary>
    [MaxLength(1000)]
    public List<string>? Ids { get; set; }

    /// <summary>The search filter when <see cref="Kind"/> is <c>filter</c>. An empty filter matches everything in the time window.</summary>
    [MaxLength(2000)]
    public string? Filter { get; set; }

    /// <summary>
    /// The time range of the filter, e.g. <c>[now-7d TO now]</c>. It is resolved to absolute UTC dates when the
    /// operation is created.
    /// </summary>
    [MaxLength(100)]
    public string? Time { get; set; }

    /// <summary>The UTC offset used to resolve <see cref="Time"/>, e.g. <c>-5h</c>.</summary>
    [MaxLength(20)]
    public string? Offset { get; set; }
}
