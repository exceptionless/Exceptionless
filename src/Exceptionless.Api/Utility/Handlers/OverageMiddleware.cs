using System;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Services;
using Foundatio.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Api.Utility {
    public sealed class OverageMiddleware {
        private readonly UsageService _usageService;
        private readonly IMetricsClient _metricsClient;
        private readonly ILogger _logger;
        private readonly RequestDelegate _next;

        public OverageMiddleware(RequestDelegate next, UsageService usageService, IMetricsClient metricsClient, ILogger<OverageMiddleware> logger) {
            _next = next;
            _usageService = usageService;
            _metricsClient = metricsClient;
            _logger = logger;
        }

        private bool IsEventPost(HttpContext context) {
            if (String.Equals(context.Request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                return new Uri(context.Request.GetDisplayUrl()).AbsolutePath.Contains("/events/submit");

            if (!String.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
                return false;

            string absolutePath = new Uri(context.Request.GetDisplayUrl()).AbsolutePath;
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

            bool tooBig = false;
            if (String.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase) && context.Request.Headers != null) {
                long size = context.Request.Headers.ContentLength.GetValueOrDefault();
                await _metricsClient.GaugeAsync(MetricNames.PostsSize, size);
                if (size > Settings.Current.MaximumEventPostSize) {
                    if (_logger.IsEnabled(LogLevel.Warning)) {
                        using (_logger.BeginScope(new ExceptionlessState().Value(size).Tag(context.Request.Headers.TryGetAndReturn(Headers.ContentEncoding))))
                            _logger.LogWarning("Event submission discarded for being too large: {@value} bytes.", size);
                    }

                    await _metricsClient.CounterAsync(MetricNames.PostsDiscarded);
                    tooBig = true;
                }
            }

            bool overLimit = await _usageService.IncrementUsageAsync(context.Request.GetDefaultOrganizationId(), context.Request.GetDefaultProjectId(), tooBig);
            // block large submissions, client should break them up or remove some of the data.
            if (tooBig) {
                context.Response.StatusCode = StatusCodes.Status413RequestEntityTooLarge;
                return;
            }

            if (overLimit) {
                await _metricsClient.CounterAsync(MetricNames.PostsBlocked);
                context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                return;
            }

            await _next(context);
        }
    }
}