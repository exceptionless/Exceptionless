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
using Exceptionless.Api.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Repositories;
using Exceptionless.Extensions;
using NLog.Fluent;

namespace Exceptionless.Api.Utility {
    public sealed class OverageHandler : DelegatingHandler {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly ICacheClient _cacheClient;
        private readonly IAppStatsClient _statsClient;

        public OverageHandler(IOrganizationRepository organizationRepository, ICacheClient cacheClient, IAppStatsClient statsClient) {
            _organizationRepository = organizationRepository;
            _cacheClient = cacheClient;
            _statsClient = statsClient;
        }

        private bool IsEventPost(HttpRequestMessage request) {
            if (request.Method != HttpMethod.Post)
                return false;

            return request.RequestUri.AbsolutePath.Contains("/events") 
                || String.Equals(request.RequestUri.AbsolutePath, "/api/v1/error", StringComparison.OrdinalIgnoreCase);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (!IsEventPost(request))
                return base.SendAsync(request, cancellationToken);

            if (_cacheClient.TryGet("ApiDisabled", false))
                return CreateResponse(request, HttpStatusCode.ServiceUnavailable, "Service Unavailable");

            // TODO: Need to handle posts to /projects/{projectId:objectid}/events should be using the specified project
            var project = request.GetDefaultProject();
            if (project == null)
                return CreateResponse(request, HttpStatusCode.Unauthorized, "Unauthorized");

            bool tooBig = false;
            if (request.Content != null && request.Content.Headers != null) {
                long size = request.Content.Headers.ContentLength.GetValueOrDefault();
                _statsClient.Gauge(StatNames.PostsSize, size);
                if (size > Settings.Current.MaximumEventPostSize) {
                    Log.Warn().Message("Event submission discarded for being too large: {0}", size).Project(project.Id).Write();
                    _statsClient.Counter(StatNames.PostsDiscarded);
                    tooBig = true;
                }
            }

            bool overLimit = _organizationRepository.IncrementUsage(project.OrganizationId, tooBig);

            // block large submissions, but return success status code so the client doesn't keep sending them
            if (tooBig)
                return CreateResponse(request, HttpStatusCode.Accepted, "Event submission discarded for being too large.");

            return overLimit ? CreateResponse(request, HttpStatusCode.PaymentRequired, "Event limit exceeded.") : base.SendAsync(request, cancellationToken);
        }

        private Task<HttpResponseMessage> CreateResponse(HttpRequestMessage request, HttpStatusCode statusCode, string message) {
            HttpResponseMessage response = request.CreateResponse(statusCode);
            response.ReasonPhrase = message;
            response.Content = new StringContent(message);

            return Task.FromResult(response);
        }
    }
}