using Exceptionless.Core.Attributes;

namespace Exceptionless.Web.Models;

public record NewToken
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
}
