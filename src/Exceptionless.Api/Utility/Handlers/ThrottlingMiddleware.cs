using System;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;
using Exceptionless.Core.Extensions;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Utility;
using Microsoft.AspNetCore.Http;

namespace Exceptionless.Api.Utility.Handlers {
    public class ThrottlingOptions {
        public Func<string, long> MaxRequestsForUserIdentifierFunc { get; set; }
        public TimeSpan Period { get; set; }
        public string Message { get; set; } = "The allowed number of requests has been exceeded.";
    }

    public class ThrottlingMiddleware {
        private readonly ICacheClient _cacheClient;
        private readonly ThrottlingOptions _options;
        private readonly RequestDelegate _next;

        public ThrottlingMiddleware(RequestDelegate next, ICacheClient cacheClient, ThrottlingOptions options) {
            _next = next;
            _cacheClient = cacheClient;
            _options = options;
        }

        protected virtual string GetUserIdentifier(HttpRequest request) {
            var authType = request.GetAuthType();
            if (authType == AuthType.Token)
                return request.GetTokenOrganizationId();

            if (authType == AuthType.User) {
                var user = request.GetUser();
                if (user != null)
                    return user.Id;
            }

            // fallback to using the IP address
            string ip = request.GetClientIpAddress();
            if (String.IsNullOrEmpty(ip) || ip == "::1")
                ip = "127.0.0.1";

            return ip;
        }

        private string GetCacheKey(string userIdentifier) {
            return String.Concat("api:", userIdentifier, ":", SystemClock.UtcNow.Floor(_options.Period).Ticks);
        }

        public async Task Invoke(HttpContext context) {
            if (IsUnthrottledRoute(context)) {
                await _next(context);
                return;
            }

            string identifier = GetUserIdentifier(context.Request);
            if (String.IsNullOrEmpty(identifier)) {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            long requestCount = 1;
            try {
                string cacheKey = GetCacheKey(identifier);
                requestCount = (long)await _cacheClient.IncrementAsync(cacheKey, 1);
                if (requestCount == 1)
                    await _cacheClient.SetExpirationAsync(cacheKey, SystemClock.UtcNow.Ceiling(_options.Period));
            } catch { }

            long maxRequests = _options.MaxRequestsForUserIdentifierFunc(identifier);
            if (requestCount > maxRequests) {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                return;
            }

            long remaining = maxRequests - requestCount;
            if (remaining < 0)
                remaining = 0;

            context.Response.OnStarting(() => {
                context.Response.Headers.Add(Headers.RateLimit, maxRequests.ToString());
                context.Response.Headers.Add(Headers.RateLimitRemaining, remaining.ToString());

                return Task.CompletedTask;
            });

            await _next(context);
        }

        private bool IsUnthrottledRoute(HttpContext context) {
            if (!String.Equals(context.Request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                return false;

            string absolutePath = context.Request.Path.Value;
            if (absolutePath.EndsWith("/"))
                absolutePath = absolutePath.Substring(0, absolutePath.Length - 1);

            return absolutePath.EndsWith("/events/session/heartbeat", StringComparison.OrdinalIgnoreCase)
                || absolutePath.EndsWith("/projects/config", StringComparison.OrdinalIgnoreCase)
                || absolutePath.StartsWith("/api/v2/push", StringComparison.OrdinalIgnoreCase);
        }
    }
}