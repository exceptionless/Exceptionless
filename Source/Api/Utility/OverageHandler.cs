using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Repositories;
using Exceptionless.Extensions;
using Foundatio.Caching;
using Foundatio.Metrics;
using NLog.Fluent;

namespace Exceptionless.Api.Utility {
    public sealed class OverageHandler : DelegatingHandler {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly ICacheClient _cacheClient;
        private readonly IMetricsClient _statsClient;

        public OverageHandler(IOrganizationRepository organizationRepository, ICacheClient cacheClient, IMetricsClient statsClient) {
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

            var project = request.GetDefaultProject();
            if (project == null)
                return CreateResponse(request, HttpStatusCode.Unauthorized, "Unauthorized");

            bool tooBig = false;
            if (request.Content != null && request.Content.Headers != null) {
                long size = request.Content.Headers.ContentLength.GetValueOrDefault();
                _statsClient.Gauge(MetricNames.PostsSize, size);
                if (size > Settings.Current.MaximumEventPostSize) {
                    Log.Warn().Message("Event submission discarded for being too large: {0}", size).Project(project.Id).Write();
                    _statsClient.Counter(MetricNames.PostsDiscarded);
                    tooBig = true;
                }
            }

            bool overLimit = _organizationRepository.IncrementUsage(project.OrganizationId, tooBig);

            // block large submissions, but return success status code so the client doesn't keep sending them
            if (tooBig)
                return CreateResponse(request, HttpStatusCode.Accepted, "Event submission discarded for being too large.");

            if (overLimit) {
                _statsClient.Counter(MetricNames.PostsBlocked);
                return CreateResponse(request, HttpStatusCode.PaymentRequired, "Event limit exceeded.");
            }

            return base.SendAsync(request, cancellationToken);
        }

        private Task<HttpResponseMessage> CreateResponse(HttpRequestMessage request, HttpStatusCode statusCode, string message) {
            HttpResponseMessage response = request.CreateResponse(statusCode);
            response.ReasonPhrase = message;
            response.Content = new StringContent(message);

            return Task.FromResult(response);
        }
    }
}