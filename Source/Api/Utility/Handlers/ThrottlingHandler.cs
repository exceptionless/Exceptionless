using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;
using Exceptionless.Core.Extensions;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;

namespace Exceptionless.Api.Utility {
    public class ThrottlingHandler : DelegatingHandler {
        private readonly ICacheClient _cacheClient;
        private readonly Func<string, long> _maxRequestsForUserIdentifier;
        private readonly TimeSpan _period;
        private readonly string _message;

        public ThrottlingHandler(ICacheClient cacheClient, Func<string, long> maxRequestsForUserIdentifier, TimeSpan period, string message = "The allowed number of requests has been exceeded.") {
            _cacheClient = cacheClient;
            _maxRequestsForUserIdentifier = maxRequestsForUserIdentifier;
            _period = period;
            _message = message;
        }

        protected virtual string GetUserIdentifier(HttpRequestMessage request) {
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
            if (String.IsNullOrEmpty(ip))
                ip = "127.0.0.1";

            return ip;
        }

        protected virtual string GetCacheKey(string userIdentifier) {
            return String.Concat("api", ":", userIdentifier, ":", DateTime.UtcNow.Floor(_period).Ticks);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            string identifier = GetUserIdentifier(request);
            if (String.IsNullOrEmpty(identifier))
                return CreateResponse(request, HttpStatusCode.Forbidden, "Could not identify client.");

            long requestCount = 1;
            try {
                string cacheKey = GetCacheKey(identifier);
                requestCount = await _cacheClient.IncrementAsync(cacheKey, 1);
                if (requestCount == 1)
                    await _cacheClient.SetExpirationAsync(cacheKey, DateTime.UtcNow.Ceiling(_period));
            } catch {}

            HttpResponseMessage response;
            long maxRequests = _maxRequestsForUserIdentifier(identifier);
            if (requestCount > maxRequests)
                response = CreateResponse(request, HttpStatusCode.Conflict, _message);
            else
                response = await base.SendAsync(request, cancellationToken);

            long remaining = maxRequests - requestCount;
            if (remaining < 0)
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
    }
}