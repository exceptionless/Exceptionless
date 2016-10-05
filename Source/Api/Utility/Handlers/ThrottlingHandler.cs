using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Metrics;
using Foundatio.Utility;

namespace Exceptionless.Api.Utility {
    public sealed class ThrottlingHandler : DelegatingHandler {
        private readonly ICacheClient _cacheClient;
        private readonly IMetricsClient _metricsClient;
        private readonly Func<string, long> _maxRequestsForUserIdentifier;
        private readonly TimeSpan _period;
        private readonly string _message;

        public ThrottlingHandler(ICacheClient cacheClient, IMetricsClient metricsClient, Func<string, long> maxRequestsForUserIdentifier, TimeSpan period, string message = "The allowed number of requests has been exceeded.") {
            _cacheClient = cacheClient;
            _metricsClient = metricsClient;
            _maxRequestsForUserIdentifier = maxRequestsForUserIdentifier;
            _period = period;
            _message = message;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (IsUnthrottledRoute(request))
                return await base.SendAsync(request, cancellationToken);

            string identifier = GetUserIdentifier(request);
            if (String.IsNullOrEmpty(identifier))
                return CreateResponse(request, HttpStatusCode.Forbidden, "Could not identify client.");

            double requestCount = 1;
            try {
                string cacheKey = GetCacheKey(identifier);
                requestCount = await _cacheClient.IncrementAsync(cacheKey, 1);
                if (requestCount == 1)
                    await _cacheClient.SetExpirationAsync(cacheKey, SystemClock.UtcNow.Ceiling(_period));
            } catch {}

            HttpResponseMessage response;
            long maxRequests = _maxRequestsForUserIdentifier(identifier);
            if (requestCount > maxRequests)
                response = CreateResponse(request, HttpStatusCode.Conflict, _message);
            else
                response = await base.SendAsync(request, cancellationToken);

            double remaining = maxRequests - requestCount;
            if (remaining == 0)
                await _metricsClient.CounterAsync(MetricNames.ThrottleLimitExceeded);
            else if (remaining < 0)
                remaining = 0;

            response.Headers.Add(ExceptionlessHeaders.RateLimit, maxRequests.ToString());
            response.Headers.Add(ExceptionlessHeaders.RateLimitRemaining, remaining.ToString());
            return response;
        }

        private HttpResponseMessage CreateResponse(HttpRequestMessage request, HttpStatusCode statusCode, string message) {
            HttpResponseMessage response = request.CreateResponse(statusCode);
            response.ReasonPhrase = message;
            response.Content = new StringContent(message);

            return response;
        }

        private string GetUserIdentifier(HttpRequestMessage request) {
            var authType = request.GetAuthType();
            if (authType == AuthType.Token)
                return request.GetDefaultOrganizationId();

            if (authType == AuthType.User) {
                var user = request.GetUser();
                if (user != null)
                    return user.Id;
            }

            // fallback to using the IP address
            var ip = request.GetClientIpAddress();
            if (String.IsNullOrEmpty(ip) || ip == "::1")
                ip = "127.0.0.1";

            return ip;
        }

        private string GetCacheKey(string userIdentifier) {
            return String.Concat("api", ":", userIdentifier, ":", SystemClock.UtcNow.Floor(_period).Ticks);
        }

        private bool IsUnthrottledRoute(HttpRequestMessage request) {
            if (request.Method != HttpMethod.Get)
                return false;

            string absolutePath = request.RequestUri.AbsolutePath;
            if (absolutePath.EndsWith("/"))
                absolutePath = absolutePath.Substring(0, absolutePath.Length - 1);

            return absolutePath.EndsWith("/events/session/heartbeat", StringComparison.OrdinalIgnoreCase)
                || absolutePath.EndsWith("/projects/config", StringComparison.OrdinalIgnoreCase)
                || absolutePath.StartsWith("/api/v2/push", StringComparison.OrdinalIgnoreCase);
        }
    }
}