using Foundatio.Repositories.Models;

namespace Exceptionless.Web.Models;

public record ViewToken : IIdentity, IHaveDates
{
    public string Id { get; set; } = null!;
    public string OrganizationId { get; set; } = null!;
    public string ProjectId { get; set; } = null!;
    public string? UserId { get; set; }
    public string? DefaultProjectId { get; set; }
    public HashSet<string> Scopes { get; set; } = null!;
    public DateTime? ExpiresUtc { get; set; }
    public string? Notes { get; set; }
    public bool IsDisabled { get; set; }
    public bool IsSuspended { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
