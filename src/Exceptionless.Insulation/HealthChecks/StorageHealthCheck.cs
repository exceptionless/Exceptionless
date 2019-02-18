using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Foundatio.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Insulation.HealthChecks {
    public class StorageHealthCheck : IHealthCheck {
        private readonly IFileStorage _storage;
        private readonly ILogger _logger;

        public StorageHealthCheck(IFileStorage storage, ILoggerFactory loggerFactory) {
            _storage = storage;
            _logger = loggerFactory.CreateLogger<StorageHealthCheck>();
        }
        
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            var sw = Stopwatch.StartNew();
            
            try {
                await _storage.GetPagedFileListAsync(1, cancellationToken: cancellationToken).AnyContext();
            } catch (Exception ex) {
                return HealthCheckResult.Unhealthy("Storage Not Working.", ex);
            } finally {
                sw.Stop();
                _logger.LogTrace("Checking storage took {Duration:g}", sw.Elapsed);
            }
            
            return HealthCheckResult.Healthy();
        }
    }
}