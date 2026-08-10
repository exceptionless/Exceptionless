using Exceptionless.Core;
using Exceptionless.Core.Services;
using Microsoft.Net.Http.Headers;

namespace Exceptionless.Web.Mcp;

public sealed class McpOriginValidationMiddleware(RequestDelegate next, AppOptions appOptions)
{
    private readonly string _canonicalOrigin = new Uri(appOptions.BaseURL).GetLeftPart(UriPartial.Authority);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(new PathString(OAuthService.McpResource.Path), StringComparison.OrdinalIgnoreCase)
            || !context.Request.Headers.TryGetValue(HeaderNames.Origin, out var origins))
        {
            await next(context);
            return;
        }

        if (origins.Count != 1 || !IsAllowedOrigin(origins[0], _canonicalOrigin))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    }

    internal static bool IsAllowedOrigin(string? origin, string canonicalOrigin)
    {
        if (String.IsNullOrWhiteSpace(origin)
            || !Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
            || !String.IsNullOrEmpty(originUri.UserInfo)
            || !String.IsNullOrEmpty(originUri.Query)
            || !String.IsNullOrEmpty(originUri.Fragment)
            || !String.Equals(originUri.AbsolutePath, "/", StringComparison.Ordinal))
        {
            return false;
        }

        return String.Equals(originUri.GetLeftPart(UriPartial.Authority), canonicalOrigin, StringComparison.OrdinalIgnoreCase);
    }
}
