using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Caching;
using Foundatio.Jobs;
using Nest;
using NLog.Fluent;
#pragma warning disable 1998

namespace Exceptionless.EventMigration {
    public class ResetDataStoresJob : JobBase {
        private readonly ICacheClient _cacheClient;
        private readonly ElasticSearchConfiguration _configuration;
        private readonly IElasticClient _elasticClient;

        public ResetDataStoresJob(ICacheClient cacheClient, ElasticSearchConfiguration configuration, IElasticClient elasticClient) {
            _cacheClient = cacheClient;
            _configuration = configuration;
            _elasticClient = elasticClient;
        }

        protected override async Task<JobResult> RunInternalAsync(CancellationToken token) {
            if (!MigrationSettings.Current.MigrationCanResetData)
                return JobResult.FailedWithMessage("Migration:CanResetData was not set in the app.config.");

            Log.Info().Message("Flushing redis cache").Write();
            _cacheClient.FlushAll();

            Log.Info().Message("Resetting elastic search").Write();
            _configuration.DeleteIndexes(_elasticClient);
            _configuration.ConfigureIndexes(_elasticClient);
            Log.Info().Message("Finished resetting elastic search...").Write();

            return JobResult.Success;
        }
    }
}