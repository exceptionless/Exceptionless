using Exceptionless.Web.Extensions;
using Exceptionless.Web.Utility;

namespace Exceptionless.Web.Api.Filters;

public class ConfigurationResponseEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);

        var httpContext = context.HttpContext;
        if (httpContext.Response.StatusCode != StatusCodes.Status200OK && httpContext.Response.StatusCode != StatusCodes.Status202Accepted)
            return result;

        var project = httpContext.Request.GetProject();
        if (project is null)
            return result;

        string headerName = Headers.ConfigurationVersion;
        if (httpContext.Request.Path.Value is not null && httpContext.Request.Path.Value.StartsWith("/api/v1"))
            headerName = Headers.LegacyConfigurationVersion;

        // add the current configuration version to the response headers so the client will know if it should update its config.
        httpContext.Response.Headers[headerName] = project.Configuration.Version.ToString();

        return result;
    }
}
