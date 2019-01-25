using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Foundatio.Jobs;
using Foundatio.Queues;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Insulation.HealthChecks {
    public class QueueHealthCheck : IHealthCheck {
        private readonly IQueue<WorkItemData> _queue;
        private readonly ILogger _logger;

        public QueueHealthCheck(IQueue<WorkItemData> queue, ILoggerFactory loggerFactory) {
            _queue = queue;
            _logger = loggerFactory.CreateLogger<QueueHealthCheck>();
        }
        
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            var sw = Stopwatch.StartNew();
            try {
                await _queue.GetQueueStatsAsync().AnyContext();
                return HealthCheckResult.Healthy();
            } catch (Exception ex) {
                return HealthCheckResult.Unhealthy("Unable to get queue stats.", ex);
            } finally {
                sw.Stop();
                _logger.LogTrace("Checking cache took {Duration:g}", sw.Elapsed);
            }
        }
    }
}