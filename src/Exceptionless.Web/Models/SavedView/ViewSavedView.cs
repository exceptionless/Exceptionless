using Exceptionless.Core.Attributes;
using Foundatio.Repositories.Models;

namespace Exceptionless.Web.Models;

public record ViewSavedView : IIdentity, IHaveDates
{
    [ObjectId]
    public string Id { get; set; } = null!;

    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    [ObjectId]
    public string? UserId { get; set; }

    [ObjectId]
    public string CreatedByUserId { get; set; } = null!;

    [ObjectId]
    public string? UpdatedByUserId { get; set; }

    public string? Filter { get; set; }
    public string? FilterDefinitions { get; set; }
    public Dictionary<string, bool>? Columns { get; set; }
    public bool IsDefault { get; set; }
    public string Name { get; set; } = null!;
    public string? Time { get; set; }
    public int Version { get; set; }
    public string ViewType { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
