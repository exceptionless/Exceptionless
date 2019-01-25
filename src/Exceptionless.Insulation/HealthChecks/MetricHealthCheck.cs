using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Foundatio.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Insulation.HealthChecks {
    public class MetricHealthCheck : IHealthCheck {
        private readonly IMetricsClient _metrics;
        private readonly ILogger _logger;

        public MetricHealthCheck(IMetricsClient metrics, ILoggerFactory loggerFactory) {
            _metrics = metrics;
            _logger = loggerFactory.CreateLogger<MetricHealthCheck>();
        }
        
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            var sw = Stopwatch.StartNew();
            try {
                 _metrics.Counter("health");
                return Task.FromResult(HealthCheckResult.Healthy());
            } catch (Exception ex) {
                return Task.FromResult(HealthCheckResult.Unhealthy("Metrics Not Working.", ex));
            } finally {
                sw.Stop();
                _logger.LogTrace("Checking metrics took {Duration:g}", sw.Elapsed);
            }
        }
    }
}