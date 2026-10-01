namespace Exceptionless.Web.Extensions;

public static class ClientAppRoutingExtensions
{
    public static IApplicationBuilder UseClientAppRedirects(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/next", StringComparison.OrdinalIgnoreCase, out var remainingPath))
            {
                // Keep redirects local even when the old URL contains repeated slashes.
                var path = new PathString("/" + remainingPath.Value?.TrimStart('/'));
                string destination = context.Request.PathBase.Add(path).ToUriComponent() + context.Request.QueryString;
                context.Response.Redirect(destination, permanent: true, preserveMethod: true);
                return;
            }

            await next(context);
        });
    }

    public static IEndpointConventionBuilder MapClientAppFallback(this IEndpointRouteBuilder endpoints)
    {
        var app = endpoints.CreateApplicationBuilder();
        app.Use(async (context, next) =>
        {
            // Unknown service routes must retain their HTTP error rather than serve the SPA.
            if ((!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)) ||
                context.Request.Path.StartsWithSegments("/api") ||
                context.Request.Path.StartsWithSegments("/docs") ||
                context.Request.Path.StartsWithSegments("/mcp") ||
                context.Request.Path.StartsWithSegments("/.well-known") ||
                context.Request.Path.StartsWithSegments("/health") ||
                context.Request.Path.StartsWithSegments("/ready") ||
                context.Request.Path.StartsWithSegments("/_app"))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Request.Path = "/index.html";
            context.SetEndpoint(null);
            await next(context);
        });
        app.UseStaticFiles();

        var fallback = app.Build();
        // Reference IDs may contain dots without identifying a static asset.
        endpoints.MapFallback("/event/by-ref/{referenceId}", fallback).ExcludeFromDescription();
        return endpoints.MapFallback("{**slug:nonfile}", fallback).ExcludeFromDescription();
    }
}
