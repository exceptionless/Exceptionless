using Exceptionless.Core.Attributes;
using Foundatio.Repositories.Models;

namespace Exceptionless.Web.Models;

public record ViewToken : IIdentity, IHaveDates
{
    [ObjectId]
    public string Id { get; set; } = null!;

    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    [ObjectId]
    public string ProjectId { get; set; } = null!;

    [ObjectId]
    public string? UserId { get; set; }

    [ObjectId]
    public string? DefaultProjectId { get; set; }
    public HashSet<string> Scopes { get; set; } = null!;
    public DateTime? ExpiresUtc { get; set; }
    public string? Notes { get; set; }
    public bool IsDisabled { get; set; }
    public bool IsSuspended { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
