using System;
using System.Threading.Tasks;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Repositories;
using MongoDB.Driver;
using Nest;

namespace Exceptionless.Core.Utility {
    public class SystemHealthChecker {
        private readonly ICacheClient _cacheClient;
        private readonly MongoDatabase _db;
        private readonly IElasticClient _elasticClient;

        public SystemHealthChecker(ICacheClient cacheClient, MongoDatabase db, IElasticClient elasticClient) {
            _cacheClient = cacheClient;
            _db = db;
            _elasticClient = elasticClient;
        }

        // TODO: Check storage
        // TODO: Check queues
        // TODO: Check message bus
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
                if (!IsDbUpToDate())
                    return HealthCheckResult.NotHealthy("Mongo DB Schema Outdated");

                if (!_db.CollectionExists(UserRepository.CollectionName))
                    return HealthCheckResult.NotHealthy("Mongo DB Missing User Collection");
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

                var url = new MongoUrl(Settings.Current.MongoConnectionString);
                string databaseName = url.DatabaseName;
                if (Settings.Current.AppendMachineNameToDatabase)
                    databaseName += String.Concat("-", Environment.MachineName.ToLower());

                _dbIsUpToDate = MongoMigrationChecker.IsUpToDate(Settings.Current.MongoConnectionString, databaseName);
                if (_dbIsUpToDate.Value)
                    return true;

                // if enabled, auto upgrade the database
                if (Settings.Current.ShouldAutoUpgradeDatabase)
                    Task.Factory.StartNew(() => MongoMigrationChecker.EnsureLatest(Settings.Current.MongoConnectionString, databaseName))
                        .ContinueWith(_ => { _dbIsUpToDate = false; });

                return false;
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
