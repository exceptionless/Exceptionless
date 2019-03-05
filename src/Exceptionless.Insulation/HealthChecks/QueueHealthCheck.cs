using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.DateTimeExtensions;
using Foundatio.Queues;
using Foundatio.Utility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Insulation.HealthChecks {
    public class QueueHealthCheck<T> : IHealthCheck where T : class {
        private readonly IQueue<T> _queue;
        private readonly ILogger _logger;

        public QueueHealthCheck(IQueue<T> queue, ILoggerFactory loggerFactory) {
            _queue = queue;
            _logger = loggerFactory.CreateLogger<T>();
        }
        
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            if (_queue is IQueueActivity qa) {
                if (qa.LastDequeueActivity.HasValue && qa.LastDequeueActivity.Value.IsBefore(SystemClock.UtcNow.SubtractMinutes(1)))
                    return HealthCheckResult.Unhealthy("Last Dequeue was over a minute ago");
                    
                return HealthCheckResult.Healthy();
            }
            
            var sw = Stopwatch.StartNew();
            try {
                await _queue.GetQueueStatsAsync().AnyContext();
                return HealthCheckResult.Healthy();
            } catch (Exception ex) {
                return HealthCheckResult.Unhealthy("Unable to get queue stats.", ex);
            } finally {
                sw.Stop();
                _logger.LogTrace("Checking queue took {Duration:g}", sw.Elapsed);
            }
        }
    }
}