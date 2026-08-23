namespace Exceptionless.Web.Models;

public sealed record ViewSavedViewDefaults
{
    public ViewSavedView? UserDefault { get; init; }
    public ViewSavedView? OrganizationDefault { get; init; }
}
