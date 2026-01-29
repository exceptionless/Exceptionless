using Exceptionless.Core.Attributes;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

public class Token : IOwnedByOrganizationAndProjectWithIdentity, IHaveDates
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
    public string? Refresh { get; set; }
    public TokenType Type { get; set; }
    public HashSet<string> Scopes { get; set; } = new();
    public DateTime? ExpiresUtc { get; set; }
    public string? Notes { get; set; }
    public bool IsDisabled { get; set; }
    public bool IsSuspended { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public enum TokenType
{
    Authentication,
    Access
}
