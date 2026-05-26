using Exceptionless.Web.Api.Endpoints;

namespace Exceptionless.Web.Api;

public static class ApiEndpoints
{
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        app.MapStatusEndpoints();
        app.MapUtilityEndpoints();
        app.MapTokenEndpoints();
        app.MapWebHookEndpoints();
        app.MapStripeEndpoints();

        return app;
    }
}
