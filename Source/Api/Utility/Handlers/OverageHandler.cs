using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Metrics;
using NLog.Fluent;

namespace Exceptionless.Api.Utility {
    public sealed class OverageHandler : DelegatingHandler {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly ICacheClient _cacheClient;
        private readonly IMetricsClient _metricsClient;

        public OverageHandler(IOrganizationRepository organizationRepository, ICacheClient cacheClient, IMetricsClient metricsClient) {
            _organizationRepository = organizationRepository;
            _cacheClient = cacheClient;
            _metricsClient = metricsClient;
        }

        private bool IsEventPost(HttpRequestMessage request) {
            if (request.Method != HttpMethod.Post)
                return false;

            return request.RequestUri.AbsolutePath.Contains("/events") 
                || String.Equals(request.RequestUri.AbsolutePath, "/api/v1/error", StringComparison.OrdinalIgnoreCase);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (!IsEventPost(request))
                return await base.SendAsync(request, cancellationToken).AnyContext();

            if (await _cacheClient.GetAsync<bool>("ApiDisabled").AnyContext())
                return CreateResponse(request, HttpStatusCode.ServiceUnavailable, "Service Unavailable");

            var project = request.GetDefaultProject();
            if (project == null)
                return CreateResponse(request, HttpStatusCode.Unauthorized, "Unauthorized");

            bool tooBig = false;
            if (request.Content != null && request.Content.Headers != null) {
                long size = request.Content.Headers.ContentLength.GetValueOrDefault();
                await _metricsClient.GaugeAsync(MetricNames.PostsSize, size).AnyContext();
                if (size > Settings.Current.MaximumEventPostSize) {
                    Log.Warn().Message("Event submission discarded for being too large: {0}", size).Project(project.Id).Write();
                    await _metricsClient.CounterAsync(MetricNames.PostsDiscarded).AnyContext();
                    tooBig = true;
                }
            }

            bool overLimit = await _organizationRepository.IncrementUsageAsync(project.OrganizationId, tooBig).AnyContext();

            // block large submissions, client should break them up or remove some of the data.
            if (tooBig)
                return CreateResponse(request, HttpStatusCode.RequestEntityTooLarge, "Event submission discarded for being too large.");

            if (overLimit) {
                await _metricsClient.CounterAsync(MetricNames.PostsBlocked).AnyContext();
                return CreateResponse(request, HttpStatusCode.PaymentRequired, "Event limit exceeded.");
            }

            return await base.SendAsync(request, cancellationToken).AnyContext();
        }

        private HttpResponseMessage CreateResponse(HttpRequestMessage request, HttpStatusCode statusCode, string message) {
            HttpResponseMessage response = request.CreateResponse(statusCode);
            response.ReasonPhrase = message;
            response.Content = new StringContent(message);

            return response;
        }
    }
}