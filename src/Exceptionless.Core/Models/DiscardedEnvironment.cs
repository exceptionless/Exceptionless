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
