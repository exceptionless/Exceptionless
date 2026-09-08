using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Attributes;

namespace Exceptionless.Core.Models;

public sealed record UserSavedViewOrderPreference
{
    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    [Required]
    public string ViewType { get; set; } = null!;

    public List<string> SavedViewIds { get; set; } = [];
}
