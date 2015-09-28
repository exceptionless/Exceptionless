using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Metrics;
using NLog.Fluent;

namespace Exceptionless.Api.Utility {
    public sealed class OverageHandler : DelegatingHandler {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ICacheClient _cacheClient;
        private readonly IMetricsClient _metricsClient;

        public OverageHandler(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, ICacheClient cacheClient, IMetricsClient metricsClient) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
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
                return await base.SendAsync(request, cancellationToken);

            if (await _cacheClient.GetAsync<bool>("ApiDisabled"))
                return CreateResponse(request, HttpStatusCode.ServiceUnavailable, "Service Unavailable");
            
            // TODO: We could make event submission even faster if we just read the project id and org id from the token.
            var project = await request.GetDefaultProjectAsync(_projectRepository);
            if (project == null)
                return CreateResponse(request, HttpStatusCode.Unauthorized, "Unauthorized");

            bool tooBig = false;
            if (request.Content?.Headers != null) {
                long size = request.Content.Headers.ContentLength.GetValueOrDefault();
                await _metricsClient.GaugeAsync(MetricNames.PostsSize, size);
                if (size > Settings.Current.MaximumEventPostSize) {
                    Log.Warn().Message("Event submission discarded for being too large: {0}", size).Project(project.Id).Write();
                    await _metricsClient.CounterAsync(MetricNames.PostsDiscarded);
                    tooBig = true;
                }
            }

            bool overLimit = await _organizationRepository.IncrementUsageAsync(project.OrganizationId, tooBig);

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