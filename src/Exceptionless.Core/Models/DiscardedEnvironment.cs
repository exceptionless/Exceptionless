namespace Exceptionless.Core.Models;

/// <summary>
/// An environment name that recently had events discarded because it is not listed on the project.
/// </summary>
public record DiscardedEnvironment
{
    /// <summary>
    /// The lowercased key for the environment, or null for events that had no environment.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// The first-seen casing of the name, or null for events that had no environment.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The number of events discarded within the retention window.
    /// </summary>
    public long Count { get; init; }

    public DateTime LastSeenUtc { get; init; }
}

/// <summary>
/// The names that recently had events discarded, plus how many events came from names that could not be listed individually.
/// </summary>
public record DiscardedEnvironmentSummary
{
    public IReadOnlyCollection<DiscardedEnvironment> Environments { get; init; } = [];

    /// <summary>
    /// Events within the retention window from names beyond the per-project limit.
    /// </summary>
    public long OtherEvents { get; init; }

    /// <summary>
    /// How many different names those events came from, up to a limit.
    /// </summary>
    public int OtherNames { get; init; }

    /// <summary>
    /// True when there were more other names than are counted.
    /// </summary>
    public bool OtherNamesCapped { get; init; }
}
