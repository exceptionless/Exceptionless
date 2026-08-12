using Exceptionless.Core;
using Exceptionless.Core.Services;
using Microsoft.Net.Http.Headers;

namespace Exceptionless.Web.Mcp;

public sealed class McpOriginValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IReadOnlySet<string> _allowedOrigins;

    public McpOriginValidationMiddleware(RequestDelegate next, AppOptions appOptions, IConfiguration configuration)
    {
        _next = next;
        _allowedOrigins = configuration.GetSection("Mcp:AllowedOrigins")
            .GetChildren()
            .Select(c => c.Value)
            .Prepend(appOptions.BaseURL)
            .Where(v => TryNormalizeOrigin(v, out _))
            .Select(v => NormalizeOrigin(v!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(new PathString(OAuthService.McpResource.Path), StringComparison.OrdinalIgnoreCase)
            || !context.Request.Headers.TryGetValue(HeaderNames.Origin, out var origins))
        {
            await _next(context);
            return;
        }

        if (origins.Count != 1 || !IsAllowedOrigin(origins[0], _allowedOrigins))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await _next(context);
    }

    internal static bool IsAllowedOrigin(string? origin, IReadOnlySet<string> allowedOrigins)
    {
        return TryNormalizeOrigin(origin, out string? normalizedOrigin)
            && allowedOrigins.Contains(normalizedOrigin);
    }

    private static string NormalizeOrigin(string origin)
    {
        return TryNormalizeOrigin(origin, out string? normalizedOrigin)
            ? normalizedOrigin
            : throw new ArgumentException("Origin must be an absolute URI authority.", nameof(origin));
    }

    private static bool TryNormalizeOrigin(string? origin, out string normalizedOrigin)
    {
        normalizedOrigin = String.Empty;
        if (String.IsNullOrWhiteSpace(origin)
            || !Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
            || !String.IsNullOrEmpty(originUri.UserInfo)
            || !String.IsNullOrEmpty(originUri.Query)
            || !String.IsNullOrEmpty(originUri.Fragment)
            || !String.Equals(originUri.AbsolutePath, "/", StringComparison.Ordinal))
        {
            return false;
        }

        normalizedOrigin = originUri.GetLeftPart(UriPartial.Authority);
        return true;
    }
}
