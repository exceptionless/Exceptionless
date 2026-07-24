using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Exceptionless.Core.Attributes;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

/// <summary>
/// A saved view captures filter, time range, and display settings for a dashboard page.
/// Org-scoped; optionally user-private when UserId is set.
/// </summary>
public partial record SavedView : IOwnedByOrganizationWithIdentity, IHaveDates
{
    public const int MaxFilterDefinitionsLength = 100_000;
    public const string SlugPattern = "^(?![a-f0-9]{24}$)[a-z0-9]+(?:-[a-z0-9]+)*$";

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
    [MaxLength(MaxFilterDefinitionsLength)]
    public string? FilterDefinitions { get; set; }

    /// <summary>Column visibility state per dashboard table, keyed by column id.</summary>
    public Dictionary<string, bool>? Columns { get; set; }

    /// <summary>Column display order per dashboard table, excluding utility columns.</summary>
    public List<string>? ColumnOrder { get; set; }

    /// <summary>Whether dashboard statistic cards are shown for this view. Null means use the default.</summary>
    public bool? ShowStats { get; set; }

    /// <summary>Whether the dashboard chart is shown for this view. Null means use the default.</summary>
    public bool? ShowChart { get; set; }

    /// <summary>Stable identifier used to synchronize predefined saved views across organizations.</summary>
    [MaxLength(150)]
    public string? PredefinedKey { get; set; }

    /// <summary>Content hash of the predefined configuration last applied to this view.</summary>
    [MaxLength(64)]
    public string? PredefinedContentHash { get; set; }

    /// <summary>Display name shown in the sidebar and picker.</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    /// <summary>URL slug used to load this saved view.</summary>
    [Required]
    [MaxLength(100)]
    [RegularExpression(SlugPattern)]
    public string Slug { get; set; } = null!;

    /// <summary>Date-math time range, e.g. "[now-7d TO now]". Null if no time constraint.</summary>
    [MaxLength(100)]
    public string? Time { get; set; }

    /// <summary>Sort expression for the dashboard table, e.g. "-date".</summary>
    [MaxLength(100)]
    public string? Sort { get; set; }

    /// <summary>Schema version for future filter definition migrations.</summary>
    public int Version { get; set; } = 1;

    /// <summary>True when the filter references at least one custom field or other premium feature.</summary>
    public bool UsesPremiumFeatures { get; set; }

    /// <summary>Dashboard page identifier: "events", "stacks", or "stream".</summary>
    [Required]
    [RegularExpression("^(events|stacks|stream)$")]
    public string ViewType { get; set; } = null!;

    // Timestamps
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    [GeneratedRegex(SlugPattern)]
    public static partial Regex SlugRegex();
}
