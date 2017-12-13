using System;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Foundatio.Metrics;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Api.Utility {
    public sealed class OverageMiddleware {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly UsageService _usageService;
        private readonly IMetricsClient _metricsClient;
        private readonly ILogger _logger;
        private readonly RequestDelegate _next;

        public OverageMiddleware(RequestDelegate next, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, UsageService usageService, IMetricsClient metricsClient, ILogger<OverageMiddleware> logger) {
            _next = next;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _usageService = usageService;
            _metricsClient = metricsClient;
            _logger = logger;
        }

        private bool IsEventPost(HttpContext context) {
            string method = context.Request.Method;
            if (String.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                return context.Request.Path.Value.Contains("/events/submit");

            if (!String.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
                return false;

            string absolutePath = context.Request.Path.Value;
            if (absolutePath.EndsWith("/"))
                absolutePath = absolutePath.Substring(0, absolutePath.Length - 1);

            return absolutePath.EndsWith("/events", StringComparison.OrdinalIgnoreCase)
                || String.Equals(absolutePath, "/api/v1/error", StringComparison.OrdinalIgnoreCase);
        }

        public async Task Invoke(HttpContext context) {
            if (!IsEventPost(context)) {
                await _next(context);
                return;
            }

            if (Settings.Current.EventSubmissionDisabled) {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }

            string organizationId = context.Request.GetDefaultOrganizationId();
            var organizationTask = _organizationRepository.GetByIdAsync(organizationId, o => o.Cache());

            string projectId = context.Request.GetDefaultProjectId();
            var projectTask = _projectRepository.GetByIdAsync(projectId, o => o.Cache());

            bool tooBig = false;
            if (String.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase) && context.Request.Headers != null) {
                if (context.Request.Headers.ContentLength.HasValue && context.Request.Headers.ContentLength.Value <= 0) {
                    //_metricsClient.Counter(MetricNames.PostsBlocked);
                    context.Response.StatusCode = StatusCodes.Status411LengthRequired;
                    await Task.WhenAll(organizationTask, projectTask);
                    return;
                }

                long size = context.Request.Headers.ContentLength.GetValueOrDefault();
                if (size > 0)
                    _metricsClient.Gauge(MetricNames.PostsSize, size);

                if (size > Settings.Current.MaximumEventPostSize) {
                    if (_logger.IsEnabled(LogLevel.Warning)) {
                        using (_logger.BeginScope(new ExceptionlessState().Value(size).Tag(context.Request.Headers.TryGetAndReturn(Headers.ContentEncoding))))
                            _logger.LogWarning("Event submission discarded for being too large: {@value} bytes.", size);
                    }

                    _metricsClient.Counter(MetricNames.PostsDiscarded);
                    tooBig = true;
                }
            }

            var organization = await organizationTask;
            var project = await projectTask;
            bool overLimit = await _usageService.IncrementUsageAsync(organization, project, tooBig);

            // block large submissions, client should break them up or remove some of the data.
            if (tooBig) {
                context.Response.StatusCode = StatusCodes.Status413RequestEntityTooLarge;
                return;
            }

            if (overLimit) {
                _metricsClient.Counter(MetricNames.PostsBlocked);
                context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                return;
            }

            context.Request.SetOrganization(organization);
            context.Request.SetProject(project);
            await _next(context);
        }
    }
}