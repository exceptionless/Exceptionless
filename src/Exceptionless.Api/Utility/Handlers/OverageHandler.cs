using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Repositories;
using Foundatio.Logging;
using Foundatio.Metrics;

namespace Exceptionless.Api.Utility {
    public sealed class OverageHandler : DelegatingHandler {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IMetricsClient _metricsClient;
        private readonly ILogger _logger;

        public OverageHandler(IOrganizationRepository organizationRepository, IMetricsClient metricsClient, ILogger<OverageHandler> logger) {
            _organizationRepository = organizationRepository;
            _metricsClient = metricsClient;
            _logger = logger;
        }

        private bool IsEventPost(HttpRequestMessage request) {
            if (request.Method == HttpMethod.Get)
                return request.RequestUri.AbsolutePath.Contains("/events/submit");

            if (request.Method != HttpMethod.Post)
                return false;

            string absolutePath = request.RequestUri.AbsolutePath;
            if (absolutePath.EndsWith("/"))
                absolutePath = absolutePath.Substring(0, absolutePath.Length - 1);

            return absolutePath.EndsWith("/events", StringComparison.OrdinalIgnoreCase)
                || String.Equals(absolutePath, "/api/v1/error", StringComparison.OrdinalIgnoreCase);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (!IsEventPost(request))
                return await base.SendAsync(request, cancellationToken);

            if (Settings.Current.EventSubmissionDisabled)
                return CreateResponse(request, HttpStatusCode.ServiceUnavailable, "Service Unavailable");

            bool tooBig = false;
            if (request.Method == HttpMethod.Post && request.Content?.Headers != null) {
                long size = request.Content.Headers.ContentLength.GetValueOrDefault();
                await _metricsClient.GaugeAsync(MetricNames.PostsSize, size);
                if (size > Settings.Current.MaximumEventPostSize) {
                    _logger.Warn().Message("Event submission discarded for being too large: {0}", size).Project(request.GetDefaultProjectId()).Write();
                    await _metricsClient.CounterAsync(MetricNames.PostsDiscarded);
                    tooBig = true;
                }
            }

            bool overLimit = await _organizationRepository.IncrementUsageAsync(request.GetDefaultOrganizationId(), tooBig);

            // block large submissions, client should break them up or remove some of the data.
            if (tooBig)
                return CreateResponse(request, HttpStatusCode.RequestEntityTooLarge, "Event submission discarded for being too large.");

            if (overLimit) {
                await _metricsClient.CounterAsync(MetricNames.PostsBlocked);
                return CreateResponse(request, HttpStatusCode.PaymentRequired, "Event limit exceeded.");
            }

            return await base.SendAsync(request, cancellationToken);
        }

        private HttpResponseMessage CreateResponse(HttpRequestMessage request, HttpStatusCode statusCode, string message) {
            HttpResponseMessage response = request.CreateResponse(statusCode);
            response.ReasonPhrase = message;
            response.Content = new StringContent(message);

            return response;
        }
    }
}