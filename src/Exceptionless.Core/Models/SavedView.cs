using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Attributes;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

/// <summary>
/// A saved view captures filter, time range, and display settings for a dashboard page.
/// Org-scoped; optionally user-private when UserId is set.
/// </summary>
public record SavedView : IOwnedByOrganizationWithIdentity, IHaveDates
{
    /// <summary>The set of valid dashboard view identifiers.</summary>
    public static readonly string[] ValidViews = ["events", "issues", "stream"];

    /// <summary>Valid column IDs per view, matching the TanStack Table column definitions.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ValidColumnIds =
        new Dictionary<string, IReadOnlySet<string>>
        {
            ["events"] = new HashSet<string> { "user", "date" },
            ["issues"] = new HashSet<string> { "status", "users", "events", "first", "last" },
            ["stream"] = new HashSet<string> { "user", "date" }
        };

    /// <summary>Union of all valid column IDs across all views.</summary>
    public static readonly IReadOnlySet<string> AllValidColumnIds =
        new HashSet<string>(ValidColumnIds.Values.SelectMany(ids => ids));

    // Identity
    [ObjectId]
    public string Id { get; set; } = null!;

    [ObjectId]
    [Required]
    public string OrganizationId { get; set; } = null!;

    // User associations
    /// <summary>When set, this view is private to the specified user. Null means org-wide.</summary>
    [ObjectId]
    public string? UserId { get; set; }

    /// <summary>The user who originally created this view.</summary>
    [ObjectId]
    [Required]
    public string CreatedByUserId { get; set; } = null!;

    /// <summary>The user who last modified this view.</summary>
    [ObjectId]
    public string? UpdatedByUserId { get; set; }

    // View configuration
    /// <summary>Raw Lucene filter query string, e.g. "(status:open OR status:regressed)". Null means no filter (show all).</summary>
    [MaxLength(2000)]
    public string? Filter { get; set; }

    /// <summary>JSON array of structured filter objects for UI chip hydration.</summary>
    [MaxLength(10000)]
    public string? FilterDefinitions { get; set; }

    /// <summary>Column visibility state per dashboard table, keyed by column id.</summary>
    public Dictionary<string, bool>? Columns { get; set; }

    /// <summary>Whether this view loads automatically when navigating to the page.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Display name shown in the sidebar and picker.</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    /// <summary>Date-math time range, e.g. "[now-7d TO now]". Null if no time constraint.</summary>
    [MaxLength(100)]
    public string? Time { get; set; }

    /// <summary>Schema version for future filter definition migrations.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Dashboard page identifier: "events", "issues", or "stream".</summary>
    [Required]
    [RegularExpression("^(events|issues|stream)$")]
    public string View { get; set; } = null!;

    // Timestamps
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
