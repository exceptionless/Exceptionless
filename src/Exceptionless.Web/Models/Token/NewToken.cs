using Exceptionless.Core.Attributes;
using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

// Keep this interface even though some user-token flows omit ProjectId; shared tenancy checks use it.
public record NewToken : IOwnedByOrganizationAndProject
{
    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    [ObjectId]
    public string? ProjectId { get; set; }

    [ObjectId]
    public string? DefaultProjectId { get; set; }
    public HashSet<string> Scopes { get; set; } = new();
    public DateTime? ExpiresUtc { get; set; }
    public string? Notes { get; set; }

    string IOwnedByProject.ProjectId
    {
        get => ProjectId ?? String.Empty;
        set => ProjectId = value;
    }
}
