using Exceptionless.Core.Attributes;
using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public record NewToken : IOwnedByOrganizationAndProject
{
    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    [ObjectId]
    public string ProjectId { get; set; } = null!;

    [ObjectId]
    public string? DefaultProjectId { get; set; }
    public HashSet<string> Scopes { get; set; } = new();
    public DateTime? ExpiresUtc { get; set; }
    public string? Notes { get; set; }
}
