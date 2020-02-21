using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Insulation.HealthChecks {
    public class ElasticsearchHealthCheck : IHealthCheck {
        private readonly ExceptionlessElasticConfiguration _config;
        private readonly ILogger _logger;

        public ElasticsearchHealthCheck(ExceptionlessElasticConfiguration config, ILoggerFactory loggerFactory) {
            _config = config;
            _logger = loggerFactory.CreateLogger<ElasticsearchHealthCheck>();
        }
        
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            var sw = Stopwatch.StartNew();
            
            try {
                var response = await _config.Client.PingAsync(ct: cancellationToken).AnyContext();
                if (!response.IsValid)
                    return HealthCheckResult.Unhealthy("Elasticsearch Ping Failed", response.OriginalException);
            } catch (Exception ex) {
                return HealthCheckResult.Unhealthy("Elasticsearch Not Working.", ex);
            } finally {
                sw.Stop();
                _logger.LogTrace("Checking Elasticsearch took {Duration:g}", sw.Elapsed);
            }

            return HealthCheckResult.Healthy();
        }
    }
}