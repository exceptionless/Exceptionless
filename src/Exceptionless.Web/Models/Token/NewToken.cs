using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public record NewToken : IOwnedByOrganizationAndProject
{
    public string OrganizationId { get; set; } = null!;
    public string ProjectId { get; set; } = null!;
    public string? DefaultProjectId { get; set; }
    public HashSet<string> Scopes { get; set; } = new();
    public DateTime? ExpiresUtc { get; set; }
    public string? Notes { get; set; }
}
