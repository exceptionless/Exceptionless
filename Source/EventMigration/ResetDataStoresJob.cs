using System;
using System.Configuration;
using System.Linq;
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
            
            ElasticSearchConfiguration.ConfigureMapping(_elasticClient, true);

            foreach (var collection in _mongoDatabase.GetCollectionNames().Where(name => !name.StartsWith("system")))
                _mongoDatabase.DropCollection(collection);

            // Create indexes
            new ApplicationRepository(_mongoDatabase);
            new OrganizationRepository(_mongoDatabase);
            new ProjectRepository(_mongoDatabase);
            new TokenRepository(_mongoDatabase);
            new WebHookRepository(_mongoDatabase);
            new UserRepository(_mongoDatabase);

            return JobResult.Success;
        }
    }
}