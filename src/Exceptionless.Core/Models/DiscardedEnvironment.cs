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

public record ProjectDiscardedEnvironments
{
    /// <summary>
    /// The number of days that discarded environment names are remembered.
    /// </summary>
    public int RetentionDays { get; init; }

    public IReadOnlyCollection<DiscardedEnvironment> Environments { get; init; } = [];

    /// <summary>
    /// Events from names beyond the per-project limit, which are counted but not listed individually.
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
