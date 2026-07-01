using Exceptionless.Core.Models;
using Exceptionless.Web.Extensions;

namespace Exceptionless.Web.Api.Infrastructure;

public static class CurrentUserAccessor
{
    public static User GetCurrentUser(HttpContext context) => context.Request.GetUser();

    public static bool CanAccessOrganization(HttpContext context, string organizationId)
        => context.Request.CanAccessOrganization(organizationId);

    public static bool IsInOrganization(HttpContext context, string? organizationId)
    {
        if (String.IsNullOrEmpty(organizationId))
            return false;

        return context.Request.IsInOrganization(organizationId);
    }

    public static ICollection<string> GetAssociatedOrganizationIds(HttpContext context)
        => context.Request.GetAssociatedOrganizationIds();

    public static bool IsGlobalAdmin(HttpContext context)
        => context.Request.IsGlobalAdmin();
}
