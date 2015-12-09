using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues.Models;
using Foundatio.Caching;
using Foundatio.Extensions;
using Foundatio.Lock;
using Foundatio.Logging;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Storage;
using Nest;
using Nito.AsyncEx;

namespace Exceptionless.Core.Utility {
    public class SystemHealthChecker {
        private readonly ICacheClient _cacheClient;
        private readonly IElasticClient _elasticClient;
        private readonly IFileStorage _storage;
        private readonly ILockProvider _locker;
        private readonly IQueue<StatusMessage> _queue;
        private readonly IMessageBus _messageBus;
        private readonly AsyncManualResetEvent _resetEvent = new AsyncManualResetEvent(false);

        public SystemHealthChecker(ICacheClient cacheClient, IElasticClient elasticClient, IFileStorage storage, IQueue<StatusMessage> queue, IMessageBus messageBus) {
            _cacheClient = new ScopedCacheClient(cacheClient, "health");
            _elasticClient = elasticClient;
            _storage = storage;
            _queue = queue;

            _messageBus = messageBus;
            _messageBus.Subscribe<StatusMessage>(m => _resetEvent.Set());
            
            _locker = new CacheLockProvider(_cacheClient, _messageBus);
        }

        public async Task<HealthCheckResult> CheckCacheAsync() {
            var sw = Stopwatch.StartNew();
            try {
                var cacheValue = await _cacheClient.GetAsync<string>("__PING__").AnyContext();
                if (cacheValue.HasValue)
                    return HealthCheckResult.NotHealthy("Cache Not Working");
            } catch (Exception ex) {
                return HealthCheckResult.NotHealthy("Cache Not Working: " + ex.Message);
            } finally {
                sw.Stop();
                Logger.Info().Message($"Checking cache took {sw.ElapsedMilliseconds}ms").Write();
            }

            return HealthCheckResult.Healthy;
        }

        public async Task<HealthCheckResult> CheckElasticsearchAsync() {
            var sw = Stopwatch.StartNew();
            try {
                var response = await _elasticClient.PingAsync().AnyContext();
                if (!response.IsValid)
                    return HealthCheckResult.NotHealthy("Elasticsearch Ping Failed");
            } catch (Exception ex) {
                return HealthCheckResult.NotHealthy("Elasticsearch Not Working: " + ex.Message);
            } finally {
                sw.Stop();
                Logger.Info().Message($"Checking Elasticsearch took {sw.ElapsedMilliseconds}ms").Write();
            }

            return HealthCheckResult.Healthy;
        }

        public async Task<HealthCheckResult> CheckStorageAsync() {
            const string path = "healthcheck.txt";

            var sw = Stopwatch.StartNew();
            try {
                if (!await _storage.ExistsAsync(path).AnyContext())
                    await _storage.SaveFileAsync(path, DateTime.UtcNow.ToString()).AnyContext();

                await _storage.DeleteFileAsync(path).AnyContext();
            } catch (Exception ex) {
                return HealthCheckResult.NotHealthy("Storage Not Working: " + ex.Message);
            } finally {
                sw.Stop();
                Logger.Info().Message($"Checking storage took {sw.ElapsedMilliseconds}ms").Write();
            }

            return HealthCheckResult.Healthy;
        }

        public async Task<HealthCheckResult> CheckQueueAsync() {
            using (var l = await _locker.AcquireAsync("health-queue", TimeSpan.FromSeconds(5))) {
                if (l == null)
                    return HealthCheckResult.Healthy;

                var message = new StatusMessage { Id = Guid.NewGuid().ToString() };

                var sw = Stopwatch.StartNew();
                var swTotal = Stopwatch.StartNew();
                try {
                    string id = await _queue.EnqueueAsync(message).AnyContext();
                    Logger.Info().Message($"Check Queue: EnqueueAsync {id} took {sw.ElapsedMilliseconds}ms").Write();

                    sw.Restart();
                    var queueStats = await _queue.GetQueueStatsAsync().AnyContext();
                    Logger.Info().Message($"Check Queue: GetQueueStatsAsync {id} took {sw.ElapsedMilliseconds}ms").Write();
                    if (queueStats.Enqueued == 0)
                        return HealthCheckResult.NotHealthy("Queue Not Working: No items were enqueued.");

                    sw.Restart();
                    var workItem = await _queue.DequeueAsync().AnyContext();
                    Logger.Info().Message($"Check Queue: DequeueAsync {id} took {sw.ElapsedMilliseconds}ms").Write();

                    if (workItem == null)
                        return HealthCheckResult.NotHealthy("Queue Not Working: No items could be dequeued.");

                    sw.Restart();
                    await workItem.CompleteAsync().AnyContext();
                    Logger.Info().Message($"Check Queue: CompleteAsync  {id} took {sw.ElapsedMilliseconds}ms").Write();

                    await _queue.DeleteQueueAsync().AnyContext();
                } catch (Exception ex) {
                    return HealthCheckResult.NotHealthy("Queue Not Working: " + ex.Message);
                } finally {
                    swTotal.Stop();
                    Logger.Info().Message($"Checking queue took {swTotal.ElapsedMilliseconds}ms").Write();
                }
            }

            return HealthCheckResult.Healthy;
        }

         public async Task<HealthCheckResult> CheckMessageBusAsync() {
            using (var l = await _locker.AcquireAsync("health-message-bus", TimeSpan.FromSeconds(5))) {
                if (l == null)
                    return HealthCheckResult.Healthy;

                var message = new StatusMessage { Id = Guid.NewGuid().ToString() };

                var sw = Stopwatch.StartNew();
                try {
                    await _messageBus.PublishAsync(message).AnyContext();
                    await Task.WhenAny(_resetEvent.WaitAsync(), TimeSpan.FromSeconds(2).ToCancellationToken().AsTask()).AnyContext();
                } catch (Exception ex) {
                    return HealthCheckResult.NotHealthy("MessageBus Not Working: " + ex.Message);
                } finally {
                    sw.Stop();
                    Logger.Info().Message($"Checking MessageBus took {sw.ElapsedMilliseconds}ms").Write();
                    _resetEvent.Reset();
                }
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

            result = await CheckQueueAsync().AnyContext();
            if (!result.IsHealthy)
                return result;

            result = await CheckMessageBusAsync().AnyContext();
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
