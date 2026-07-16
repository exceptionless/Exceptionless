using Exceptionless.Web.Extensions;
using Exceptionless.Web.Utility;

namespace Exceptionless.Web.Api.Filters;

public class ConfigurationResponseEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);

        // In Minimal API filters, the IResult hasn't been executed yet so httpContext.Response.StatusCode
        // is still the default. Inspect the result object's status code directly.
        if (result is not IStatusCodeHttpResult
            { StatusCode: StatusCodes.Status200OK or StatusCodes.Status202Accepted })
            return result;

        var httpContext = context.HttpContext;
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
