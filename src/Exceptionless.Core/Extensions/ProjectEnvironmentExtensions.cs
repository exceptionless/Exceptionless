using Exceptionless.Core.Models;

namespace Exceptionless.Core.Extensions;

/// <summary>
/// The single place that decides how a project treats an event's environment. Matching is always case-insensitive.
/// A project with no listed environments and default settings discards nothing and notifies for everything.
/// </summary>
public static class ProjectEnvironmentExtensions
{
    public static ProjectEnvironment? FindEnvironment(this Project project, string? environment)
    {
        if (String.IsNullOrEmpty(environment) || project.Environments is not { Count: > 0 })
            return null;

        foreach (var listed in project.Environments)
        {
            if (ProjectEnvironment.NamesMatch(listed.Name, environment))
                return listed;
        }

        return null;
    }

    /// <summary>
    /// Returns the listed casing for a listed environment, otherwise the name as it was given.
    /// </summary>
    public static string? GetEnvironmentDisplayName(this Project project, string? environment)
    {
        return project.FindEnvironment(environment)?.Name ?? environment;
    }

    /// <summary>
    /// Events without an environment are only discarded when unlisted environments are discarded as well.
    /// </summary>
    public static bool ShouldDiscardEnvironment(this Project project, string? environment)
    {
        if (!project.DiscardUnlistedEnvironments)
            return false;

        if (String.IsNullOrEmpty(environment))
            return project.DiscardEventsWithoutEnvironment;

        return project.FindEnvironment(environment) is null;
    }

    /// <summary>
    /// Listed environments use their own setting. Unlisted environments and events without an environment use the project setting.
    /// </summary>
    public static bool ShouldNotifyForEnvironment(this Project project, string? environment)
    {
        return project.FindEnvironment(environment)?.Notify ?? project.NotifyOtherEnvironments;
    }
}
