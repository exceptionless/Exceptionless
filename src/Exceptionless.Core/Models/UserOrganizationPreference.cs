using Exceptionless.Core.Attributes;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

public sealed record UserOrganizationPreference
{
    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    [ObjectId]
    public string? DefaultSavedViewId { get; set; }

    public Dictionary<string, List<string>> SavedViewOrder { get; set; } = [];
}
