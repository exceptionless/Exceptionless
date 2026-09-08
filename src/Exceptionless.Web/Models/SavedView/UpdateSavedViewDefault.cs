using Exceptionless.Core.Attributes;

namespace Exceptionless.Web.Models;

public sealed record UpdateSavedViewDefault
{
    [ObjectId]
    public string? SavedViewId { get; init; }
}
