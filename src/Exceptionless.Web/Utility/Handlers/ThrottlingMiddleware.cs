using Exceptionless.Core.Extensions;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Extensions;
using Foundatio.Caching;
using Foundatio.Utility;

namespace Exceptionless.Web.Utility.Handlers;

public record ThrottlingOptions
{
    public required Func<string, long> MaxRequestsForUserIdentifierFunc { get; set; }
    public required TimeSpan Period { get; set; }
    public string Message { get; set; } = "The allowed number of requests has been exceeded.";
}

public class ThrottlingMiddleware
{
    private readonly ICacheClient _cacheClient;
    private readonly ThrottlingOptions _options;
    private readonly RequestDelegate _next;
    private static readonly PathString _v1ProjectConfigPath = new("/api/v1/project/config");
    private static readonly PathString _v2ProjectConfigPath = new("/api/v2/projects/config");
    private static readonly PathString _heartbeatPath = new("/api/v2/events/session/heartbeat");
    private static readonly PathString _webSocketPath = new("/api/v2/push");


    public ThrottlingMiddleware(RequestDelegate next, ICacheClient cacheClient, ThrottlingOptions options)
    {
        _next = next;
        _cacheClient = cacheClient;
        _options = options;
    }

    protected virtual string? GetUserIdentifier(HttpRequest request)
    {
        var authType = request.GetAuthType();
        if (authType == AuthType.Token)
            return request.GetTokenOrganizationId();

        if (authType == AuthType.User)
        {
            var user = request.GetUser();
            if (user is not null)
                return user.Id;
        }

        // fallback to using the IP address
        string? ip = request.GetClientIpAddress();
        if (String.IsNullOrEmpty(ip) || ip == "::1")
            ip = "127.0.0.1";

        return ip;
    }

    private string GetCacheKey(string userIdentifier)
    {
        return String.Concat("api:", userIdentifier, ":", SystemClock.UtcNow.Floor(_options.Period).Ticks);
    }

    public async Task Invoke(HttpContext context)
    {
        if (IsUnthrottledRoute(context))
        {
            await _next(context);
            return;
        }

        string? identifier = GetUserIdentifier(context.Request);
        if (String.IsNullOrEmpty(identifier))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        long requestCount = 1;
        try
        {
            string cacheKey = GetCacheKey(identifier);
            requestCount = await _cacheClient.IncrementAsync(cacheKey, 1);
            if (requestCount == 1)
                await _cacheClient.SetExpirationAsync(cacheKey, SystemClock.UtcNow.Ceiling(_options.Period));
        }
        catch { }

        long maxRequests = _options.MaxRequestsForUserIdentifierFunc(identifier);
        if (requestCount > maxRequests)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return;
        }

        long remaining = maxRequests - requestCount;
        if (remaining < 0)
            remaining = 0;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Headers.RateLimit] = maxRequests.ToString();
            context.Response.Headers[Headers.RateLimitRemaining] = remaining.ToString();

            return Task.CompletedTask;
        });

        await _next(context);
    }

    private bool IsUnthrottledRoute(HttpContext context)
    {
        if (!context.Request.Method.Equals(HttpMethods.Get, StringComparison.Ordinal))
            return false;

        return context.Request.Path.StartsWithSegments(_v2ProjectConfigPath, StringComparison.Ordinal)
           || context.Request.Path.StartsWithSegments(_heartbeatPath, StringComparison.Ordinal)
           || context.Request.Path.StartsWithSegments(_webSocketPath, StringComparison.Ordinal)
           || context.Request.Path.StartsWithSegments(_v1ProjectConfigPath, StringComparison.Ordinal);
    }
}
