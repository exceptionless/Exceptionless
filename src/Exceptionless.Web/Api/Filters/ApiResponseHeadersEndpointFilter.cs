namespace Exceptionless.Web.Api.Filters;

/// <summary>
/// Endpoint filter that adds common API response headers.
/// </summary>
public class ApiResponseHeadersEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);

        // Headers that apply to all API responses can be added here
        var httpContext = context.HttpContext;
        httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";

        return result;
    }
}
