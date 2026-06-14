using Exceptionless.Web.Api.Endpoints;
using Foundatio.Mediator;

namespace Exceptionless.Web.Api;

public static class ApiEndpoints
{
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        app.MapStatusEndpoints();
        app.MapUtilityEndpoints();
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
        app.MapEventEndpoints();
        app.MapMediatorEndpoints();

        return app;
    }
}
