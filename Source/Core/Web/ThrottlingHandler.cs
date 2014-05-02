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
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Web {
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
            var project = request.GetUserPrincipal().GetProject();
            if(project != null)
                return project.OrganizationId;

            var user = request.GetUserPrincipal().GetUser();
            if(user != null)
                return user.Id;

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
            long requestCount = _cacheClient.Increment(cacheKey, 1);
            if (requestCount == 1)
                _cacheClient.SetExpiration(cacheKey, _period);

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
                httpResponse.Headers.Add(ExceptionlessHeaders.RateLimit, maxRequests.ToString());
                httpResponse.Headers.Add(ExceptionlessHeaders.RateLimitRemaining, remaining.ToString());

                return httpResponse;
            }, cancellationToken);
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