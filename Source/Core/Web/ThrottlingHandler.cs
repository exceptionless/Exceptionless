#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Authorization;
using ServiceStack.Redis;

namespace Exceptionless.Core.Web {
    public class ThrottlingHandler : DelegatingHandler {
        private readonly IRedisClientsManager _clientsManager;
        private readonly Func<string, long> _maxRequestsForUserIdentifier;
        private readonly TimeSpan _period;
        private readonly string _message;

        public ThrottlingHandler(IRedisClientsManager clientsManager, Func<string, long> maxRequestsForUserIdentifier, TimeSpan period)
            : this(clientsManager, maxRequestsForUserIdentifier, period, "The allowed number of requests has been exceeded.") {}

        public ThrottlingHandler(IRedisClientsManager clientsManager, Func<string, long> maxRequestsForUserIdentifier, TimeSpan period, string message) {
            _clientsManager = clientsManager;
            _maxRequestsForUserIdentifier = maxRequestsForUserIdentifier;
            _period = period;
            _message = message;
        }

        protected virtual string GetUserIdentifier(HttpRequestMessage request) {
            HttpRequestContext ctx = request.GetRequestContext();
            if (ctx != null) {
                // use organization id or user id as the identifier for throttling
                var principal = request.GetRequestContext().Principal as ExceptionlessPrincipal;
                if (principal != null)
                    return principal.Project != null ? principal.Project.OrganizationId : principal.UserEntity.Id;
            }
            // fallback to using the IP address
            var ip = request.GetClientIpAddress();
            return Settings.Current.WebsiteMode == WebsiteMode.Dev && String.IsNullOrEmpty(ip) ? "127.0.0.1" : ip;
        }

        protected virtual string GetCacheKey(string userIdentifier) {
            return String.Concat("api", ":", userIdentifier, ":", DateTime.UtcNow.Floor(_period).Ticks);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            string identifier = GetUserIdentifier(request);
            if (String.IsNullOrEmpty(identifier))
                return CreateResponse(request, HttpStatusCode.Forbidden, "Could not identify client.");

            string cacheKey = GetCacheKey(identifier);
            long requestCount = 0;
            using (IRedisClient client = _clientsManager.GetClient()) {
                requestCount = client.IncrementValueBy(cacheKey, 1);
                if (requestCount == 1)
                    client.ExpireEntryIn(cacheKey, _period);
            }

            Task<HttpResponseMessage> response = null;
            long maxRequests = _maxRequestsForUserIdentifier(identifier);
            if (requestCount > maxRequests)
                response = CreateResponse(request, HttpStatusCode.Conflict, _message);
            else
                response = base.SendAsync(request, cancellationToken);

            return response.ContinueWith(task => {
                long remaining = maxRequests - requestCount;
                if (remaining < 0)
                    remaining = 0;

                HttpResponseMessage httpResponse = task.Result;
                httpResponse.Headers.Add("RateLimit-Limit", maxRequests.ToString());
                httpResponse.Headers.Add("RateLimit-Remaining", remaining.ToString());

                return httpResponse;
            });
        }

        protected Task<HttpResponseMessage> CreateResponse(HttpRequestMessage request, HttpStatusCode statusCode, string message) {
            var tsc = new TaskCompletionSource<HttpResponseMessage>();
            HttpResponseMessage response = request.CreateResponse(statusCode);
            response.ReasonPhrase = message;
            response.Content = new StringContent(message);
            tsc.SetResult(response);
            return tsc.Task;
        }
    }
}