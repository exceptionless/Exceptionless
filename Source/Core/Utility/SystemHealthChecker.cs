using System;
using System.Threading;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Queues.Models;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Storage;
using MongoDB.Driver;
using Nest;

namespace Exceptionless.Core.Utility {
    public class SystemHealthChecker {
        private readonly ICacheClient _cacheClient;
        private readonly MongoDatabase _db;
        private readonly IElasticClient _elasticClient;
        private readonly IFileStorage _storage;
        private readonly IQueue<StatusMessage> _queue;
        private readonly IMessageBus _messageBus;

        public SystemHealthChecker(ICacheClient cacheClient, MongoDatabase db, IElasticClient elasticClient, IFileStorage storage, IQueue<StatusMessage> queue, IMessageBus messageBus) {
            _cacheClient = cacheClient;
            _db = db;
            _elasticClient = elasticClient;
            _storage = storage;
            _queue = queue;
            _messageBus = messageBus;
        }
    
        public HealthCheckResult CheckCache() {
            try {
                if (_cacheClient.Get<string>("__PING__") != null)
                    return HealthCheckResult.NotHealthy("Cache Not Working");
            } catch (Exception ex) {
                return HealthCheckResult.NotHealthy("Cache Not Working: " + ex.Message);
            }

            return HealthCheckResult.Healthy;
        }

        public HealthCheckResult CheckMongo() {
            try {
                _db.Server.Ping();

                if (!IsDbUpToDate())
                    return HealthCheckResult.NotHealthy("Mongo DB Schema Outdated");
            } catch (Exception ex) {
                return HealthCheckResult.NotHealthy("Mongo Not Working: " + ex.Message);
            }

            return HealthCheckResult.Healthy;
        }

        public HealthCheckResult CheckElasticSearch() {
            try {
                var res = _elasticClient.Ping();
                if (!res.IsValid)
                    return HealthCheckResult.NotHealthy("ElasticSearch Ping Failed");
            } catch (Exception ex) {
                return HealthCheckResult.NotHealthy("ElasticSearch Not Working: " + ex.Message);
            }

            return HealthCheckResult.Healthy;
        }

        public HealthCheckResult CheckStorage() {
            try {
                _storage.GetFileList(limit: 1);
            } catch (Exception ex) {
                return HealthCheckResult.NotHealthy("Storage Not Working: " + ex.Message);
            }

            return HealthCheckResult.Healthy;
        }

        public HealthCheckResult CheckQueue() {
            var message = new StatusMessage { Id = Guid.NewGuid().ToString() };
            try {
                _queue.Enqueue(message);
                if (_queue.GetQueueCount() == 0)
                    return HealthCheckResult.NotHealthy("Queue Not Working: No items were enqueued.");
      
                var workItem = _queue.Dequeue(TimeSpan.Zero);
                if (workItem == null)
                    return HealthCheckResult.NotHealthy("Queue Not Working: No items could be dequeued.");

                workItem.Complete();
            } catch (Exception ex) {
                return HealthCheckResult.NotHealthy("Queues Not Working: " + ex.Message);
            }

            return HealthCheckResult.Healthy;
        }

         public HealthCheckResult CheckMessageBus() {
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
        
             return HealthCheckResult.Healthy;
        }
    
        public HealthCheckResult CheckAll() {
            var result = CheckCache();
            if (!result.IsHealthy)
                return result;

            result = CheckMongo();
            if (!result.IsHealthy)
                return result;

            result = CheckElasticSearch();
            if (!result.IsHealthy)
                return result;

            result = CheckStorage();
            if (!result.IsHealthy)
                return result;

            result = CheckQueue();
            if (!result.IsHealthy)
                return result;

            result = CheckMessageBus();
            if (!result.IsHealthy)
                return result;

            return HealthCheckResult.Healthy;
        }

        private static bool? _dbIsUpToDate;
        private static DateTime _lastDbUpToDateCheck;
        private static readonly object _dbIsUpToDateLock = new object();

        public static bool IsDbUpToDate() {
            lock (_dbIsUpToDateLock) {
                if (_dbIsUpToDate.HasValue && (_dbIsUpToDate.Value || DateTime.Now.Subtract(_lastDbUpToDateCheck).TotalSeconds < 10))
                    return _dbIsUpToDate.Value;

                _lastDbUpToDateCheck = DateTime.Now;
                _dbIsUpToDate = MongoMigrationChecker.IsUpToDate(Settings.Current.MongoConnectionString, Settings.Current.MongoDatabaseName);

                return _dbIsUpToDate.Value;
            }
        }
    }

    public class HealthCheckResult {
        public bool IsHealthy { get; set; }
        public string Message { get; set; }

        private static readonly HealthCheckResult _healthy = new HealthCheckResult {
            IsHealthy = true
        };

        public static HealthCheckResult Healthy { get { return _healthy; } }

        public static HealthCheckResult NotHealthy(string message = null) {
            return new HealthCheckResult { IsHealthy = false, Message = message };
        }
    }
}
