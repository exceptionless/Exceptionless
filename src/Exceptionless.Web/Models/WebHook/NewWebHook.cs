using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public record NewWebHook : IOwnedByOrganizationAndProject
{
    public string OrganizationId { get; set; } = null!;
    public string ProjectId { get; set; } = null!;
    public string Url { get; set; } = null!;
    public string[] EventTypes { get; set; } = null!;

    /// <summary>
    /// The schema version that should be used.
    /// </summary>
    public Version? Version { get; set; }
}
