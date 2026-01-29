using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Attributes;
using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public record NewWebHook : IOwnedByOrganizationAndProject
{
    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    [ObjectId]
    public string ProjectId { get; set; } = null!;

    [Url]
    public string Url { get; set; } = null!;
    public string[] EventTypes { get; set; } = null!;

    /// <summary>
    /// The schema version that should be used.
    /// </summary>
    public Version? Version { get; set; }
}
