using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Foundatio.Caching;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Insulation.HealthChecks {
    public class CacheHealthCheck : IHealthCheck {
        private readonly ICacheClient _cache;
        private readonly ILogger _logger;

        public CacheHealthCheck(ICacheClient cache, ILoggerFactory loggerFactory) {
            _cache = cache;
            _logger = loggerFactory.CreateLogger<CacheHealthCheck>();
        }
        
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            var sw = Stopwatch.StartNew();
            try {
                var cache = new ScopedCacheClient(_cache, "health");
                var cacheValue = await cache.GetAsync<string>("__PING__").AnyContext();
                if (cacheValue.HasValue)
                    return HealthCheckResult.Unhealthy("Cache Not Working");
            } catch (Exception ex) {
                return HealthCheckResult.Unhealthy("Cache Not Working.", ex);
            } finally {
                sw.Stop();
                _logger.LogTrace("Checking cache took {Duration:g}", sw.Elapsed);
            }
            
            return HealthCheckResult.Healthy();
        }
    }
}
