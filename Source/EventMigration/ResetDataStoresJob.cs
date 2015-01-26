using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using MongoDB.Driver;
using Nest;

namespace Exceptionless.EventMigration {
    public class ResetDataStoresJob : JobBase {
        private readonly ICacheClient _cacheClient;
        private readonly IElasticClient _elasticClient;
        private readonly MongoDatabase _mongoDatabase;

        public ResetDataStoresJob(ICacheClient cacheClient, IElasticClient elasticClient, MongoDatabase mongoDatabase) {
            _cacheClient = cacheClient;
            _elasticClient = elasticClient;
            _mongoDatabase = mongoDatabase;
        }

        protected override async Task<JobResult> RunInternalAsync(CancellationToken token) {
            if (!ConfigurationManager.AppSettings.GetBool("Migration:CanResetData").GetValueOrDefault())
                return JobResult.FailedWithMessage("Migration:CanResetData was not set in the app.config.");

            _cacheClient.FlushAll();
            
            _elasticClient.DeleteIndex(i => i.AllIndices());
            _elasticClient.DeleteTemplate(ElasticSearchRepository<PersistentEvent>.EventsIndexName);

            // Old collections
            _mongoDatabase.DropCollection("_schemaversion");
            _mongoDatabase.DropCollection("error");
            _mongoDatabase.DropCollection("errorstack");
            _mongoDatabase.DropCollection("errorstack.stats.day");
            _mongoDatabase.DropCollection("errorstack.stats.month");
            _mongoDatabase.DropCollection("jobhistory");
            _mongoDatabase.DropCollection("joblock");
            _mongoDatabase.DropCollection("organization");
            _mongoDatabase.DropCollection("project");
            _mongoDatabase.DropCollection("project.stats.day");
            _mongoDatabase.DropCollection("project.stats.month");
            _mongoDatabase.DropCollection("project.hook");
            _mongoDatabase.DropCollection("user");

            // New Collections
            _mongoDatabase.DropCollection("application");
            _mongoDatabase.DropCollection("token");
            _mongoDatabase.DropCollection("webhook");

            return JobResult.Success;
        }
    }
}