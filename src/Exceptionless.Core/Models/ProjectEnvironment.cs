using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Core.Models;

/// <summary>
/// A deployment environment that a project has chosen to list, such as production or staging.
/// </summary>
public class ProjectEnvironment
{
    /// <summary>
    /// The maximum number of environments that can be listed on a project.
    /// </summary>
    public const int MaximumCount = 100;

    /// <summary>
    /// The environment name in the exact casing that was entered. Matching against event environments is case-insensitive.
    /// </summary>
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// If true, events from this environment can send notifications.
    /// </summary>
    public bool Notify { get; set; } = true;

    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Returns the trimmed name, or null when the value cannot be an environment name. Uses the same rules as <see cref="Event.Environment"/>.
    /// </summary>
    public static string? NormalizeName(string? value) => Event.NormalizeEnvironment(value);

    /// <summary>
    /// Returns the case-insensitive key for an environment name. This matches the lowercased keys returned by environment aggregations.
    /// </summary>
    public static string GetKey(string name) => name.ToLowerInvariant();

    public static bool NamesMatch(string? first, string? second) => String.Equals(first, second, StringComparison.OrdinalIgnoreCase);
}
