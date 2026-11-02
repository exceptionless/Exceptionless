using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public record NewProjectEnvironment
{
    /// <summary>
    /// The environment name. It is trimmed, limited to 64 characters, and is matched case-insensitively against event environments.
    /// </summary>
    [Required]
    public string Name { get; set; } = null!;

    /// <summary>
    /// If true, events from this environment can send notifications.
    /// </summary>
    public bool Notify { get; set; } = true;
}

public record UpdateProjectEnvironment
{
    /// <summary>
    /// If true, events from this environment can send notifications.
    /// </summary>
    [Required]
    public bool? Notify { get; set; }
}

public record UpdateProjectEnvironmentSettings
{
    /// <summary>
    /// If true, events whose environment is not listed are discarded.
    /// </summary>
    public bool? DiscardUnlistedEnvironments { get; set; }

    /// <summary>
    /// If true, events without an environment are also discarded. Only applies while unlisted environments are discarded.
    /// </summary>
    public bool? DiscardEventsWithoutEnvironment { get; set; }

    /// <summary>
    /// If true, events from environments that are not listed and events without an environment can send notifications.
    /// </summary>
    public bool? NotifyOtherEnvironments { get; set; }
}

public record ProjectEnvironmentSettings
{
    public List<ProjectEnvironment> Environments { get; set; } = [];
    public bool DiscardUnlistedEnvironments { get; set; }
    public bool DiscardEventsWithoutEnvironment { get; set; }
    public bool NotifyOtherEnvironments { get; set; } = true;
}

public record ProjectDiscardedEnvironments
{
    /// <summary>
    /// The number of days that discarded environment names are remembered.
    /// </summary>
    public int RetentionDays { get; set; }

    public IReadOnlyCollection<DiscardedEnvironment> Environments { get; set; } = [];
}
