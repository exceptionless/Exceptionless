using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Caching;
using Foundatio.Storage;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Utility {
    public class SystemHealthChecker {
        private readonly ExceptionlessElasticConfiguration _configuration;
        private readonly IFileStorage _storage;
        private readonly ILogger _logger;

        public SystemHealthChecker(ExceptionlessElasticConfiguration configuration, IFileStorage storage, ILogger<SystemHealthChecker> logger) {
            _configuration = configuration;
            _storage = storage;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckCacheAsync() {
            var sw = Stopwatch.StartNew();
            try {
                var cache = new ScopedCacheClient(_configuration.Cache, "health");
                var cacheValue = await cache.GetAsync<string>("__PING__").AnyContext();
                if (cacheValue.HasValue)
                    return HealthCheckResult.NotHealthy("Cache Not Working");
            } catch (Exception ex) {
                return HealthCheckResult.NotHealthy("Cache Not Working: " + ex.Message);
            } finally {
                sw.Stop();
                _logger.LogTrace("Checking cache took {Duration:g}", sw.Elapsed);
            }

            return HealthCheckResult.Healthy;
        }

        public async Task<HealthCheckResult> CheckElasticsearchAsync() {
            var sw = Stopwatch.StartNew();
            try {
                var response = await _configuration.Client.PingAsync().AnyContext();
                if (!response.IsValid)
                    return HealthCheckResult.NotHealthy("Elasticsearch Ping Failed");
            } catch (Exception ex) {
                return HealthCheckResult.NotHealthy("Elasticsearch Not Working: " + ex.Message);
            } finally {
                sw.Stop();
                _logger.LogTrace("Checking Elasticsearch took {Duration:g}", sw.Elapsed);
            }

            return HealthCheckResult.Healthy;
        }

        public async Task<HealthCheckResult> CheckStorageAsync() {
            const string path = "healthcheck.txt";

            var sw = Stopwatch.StartNew();
            try {
                if (!await _storage.ExistsAsync(path).AnyContext())
                    await _storage.SaveFileAsync(path, SystemClock.UtcNow.ToString()).AnyContext();

                await _storage.DeleteFileAsync(path).AnyContext();
            } catch (Exception ex) {
                return HealthCheckResult.NotHealthy("Storage Not Working: " + ex.Message);
            } finally {
                sw.Stop();
                _logger.LogTrace("Checking storage took {Duration:g}", sw.Elapsed);
            }

            return HealthCheckResult.Healthy;
        }
        
        public async Task<HealthCheckResult> CheckAllAsync() {
            var result = await CheckCacheAsync().AnyContext();
            if (!result.IsHealthy)
                return result;
            
            result = await CheckElasticsearchAsync().AnyContext();
            if (!result.IsHealthy)
                return result;

            result = await CheckStorageAsync().AnyContext();
            if (!result.IsHealthy)
                return result;
            
            return HealthCheckResult.Healthy;
        }
    }

    public class HealthCheckResult {
        public bool IsHealthy { get; set; }
        public string Message { get; set; }

        public static HealthCheckResult Healthy { get; } = new HealthCheckResult {
            IsHealthy = true
        };

        public static HealthCheckResult NotHealthy(string message = null) {
            return new HealthCheckResult { IsHealthy = false, Message = message };
        }
    }
}