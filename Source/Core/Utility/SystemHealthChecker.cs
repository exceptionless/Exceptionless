using System;
using System.IO;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues.Models;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Storage;
using Nest;

namespace Exceptionless.Core.Utility {
    public class SystemHealthChecker {
        private readonly ICacheClient _cacheClient;
        private readonly IElasticClient _elasticClient;
        private readonly IFileStorage _storage;
        private readonly IQueue<StatusMessage> _queue;
        private readonly IMessageBus _messageBus;

        public SystemHealthChecker(ICacheClient cacheClient, IElasticClient elasticClient, IFileStorage storage, IQueue<StatusMessage> queue, IMessageBus messageBus) {
            _cacheClient = cacheClient;
            _elasticClient = elasticClient;
            _storage = storage;
            _queue = queue;
            _messageBus = messageBus;
        }

        public async Task<HealthCheckResult> CheckCacheAsync() {
            try {
                var cacheValue = await _cacheClient.GetAsync<string>("__PING__").AnyContext();
                if (cacheValue.HasValue)
                    return HealthCheckResult.NotHealthy("Cache Not Working");
            } catch (Exception ex) {
                return HealthCheckResult.NotHealthy("Cache Not Working: " + ex.Message);
            }

            return HealthCheckResult.Healthy;
        }

        public async Task<HealthCheckResult> CheckElasticSearchAsync() {
            try {
                var response = await _elasticClient.PingAsync().AnyContext();
                if (!response.IsValid)
                    return HealthCheckResult.NotHealthy("ElasticSearch Ping Failed");
            } catch (Exception ex) {
                return HealthCheckResult.NotHealthy("ElasticSearch Not Working: " + ex.Message);
            }

            return HealthCheckResult.Healthy;
        }

        public async Task<HealthCheckResult> CheckStorageAsync() {
            const string path = "healthcheck.txt";

            try {
                if (!await _storage.ExistsAsync(path).AnyContext())
                    await _storage.SaveFileAsync(path, DateTime.UtcNow.ToString()).AnyContext();

                await _storage.DeleteFileAsync(path).AnyContext();
            } catch (Exception ex) {
                return HealthCheckResult.NotHealthy("Storage Not Working: " + ex.Message);
            }

            return HealthCheckResult.Healthy;
        }

        public async Task<HealthCheckResult> CheckQueueAsync() {
            var message = new StatusMessage { Id = Guid.NewGuid().ToString() };
            try {
                await _queue.EnqueueAsync(message).AnyContext();

                var queueStats = await _queue.GetQueueStatsAsync().AnyContext();
                if (queueStats.Enqueued == 0)
                    return HealthCheckResult.NotHealthy("Queue Not Working: No items were enqueued.");

                var workItem = await _queue.DequeueAsync().AnyContext();
                if (workItem == null)
                    return HealthCheckResult.NotHealthy("Queue Not Working: No items could be dequeued.");

                await workItem.CompleteAsync().AnyContext();
            } catch (Exception ex) {
                return HealthCheckResult.NotHealthy("Queues Not Working: " + ex.Message);
            }

            return HealthCheckResult.Healthy;
        }

         public Task<HealthCheckResult> CheckMessageBusAsync() {
            //var message = new StatusMessage { Id = Guid.NewGuid().ToString() };
            //var resetEvent = new AutoResetEvent(false);
            //Action<StatusMessage> handler = msg => resetEvent.Set();

             //try {
             //    _messageBus.Subscribe(handler);
             //    _messageBus.Publish(message);
             //    bool success = resetEvent.WaitOne(5000);
             //    if (!success)
             //        return HealthCheckResult.NotHealthy("MessageBus Not Working: Failed to receive message.");
             //} catch (Exception ex) {
             //    return HealthCheckResult.NotHealthy("MessageBus Not Working: " + ex.Message);
             //} finally {
             //    _messageBus.Unsubscribe(handler);
             //}

             return Task.FromResult(HealthCheckResult.Healthy);
        }

        public async Task<HealthCheckResult> CheckAllAsync() {
            var result = await CheckCacheAsync().AnyContext();
            if (!result.IsHealthy)
                return result;

            result = await CheckElasticSearchAsync().AnyContext();
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
