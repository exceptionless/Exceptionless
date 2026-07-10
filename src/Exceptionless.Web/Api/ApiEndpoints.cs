using Exceptionless.Web.Api.Endpoints;

namespace Exceptionless.Web.Api;

public static class ApiEndpoints
{
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        app.MapStatusEndpoints();
        app.MapUtilityEndpoints();
        app.MapContactEndpoints();
        app.MapAuthEndpoints();
        app.MapTokenEndpoints();
        app.MapWebHookEndpoints();
        app.MapStripeEndpoints();
        app.MapSavedViewEndpoints();
        app.MapUserEndpoints();
        app.MapProjectEndpoints();
        app.MapOrganizationEndpoints();
        app.MapStackEndpoints();
        app.MapAdminEndpoints();
        app.MapOAuthApplicationEndpoints();
        app.MapOAuthEndpoints();
        app.MapEventEndpoints();
        return app;
    }
}
