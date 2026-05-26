using Exceptionless.Core.Authorization;

namespace Exceptionless.Web.Api;

public static class ApiEndpointGroups
{
    public static RouteGroupBuilder MapApiGroup(this IEndpointRouteBuilder routes, string prefix)
    {
        return routes.MapGroup($"api/v2/{prefix}")
            .RequireAuthorization(AuthorizationRoles.UserPolicy);
    }

    public static RouteGroupBuilder MapApiGroup(this IEndpointRouteBuilder routes)
    {
        return routes.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.UserPolicy);
    }
}
