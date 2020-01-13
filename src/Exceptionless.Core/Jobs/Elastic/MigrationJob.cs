using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Jobs;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.Elastic {
    [Job(Description = "Runs any pending document migrations.", IsContinuous = false)]
    public class MigrationJob : JobBase {
        private readonly MigrationManager _migrationManager;
        private readonly ExceptionlessElasticConfiguration _configuration;

        public MigrationJob(ILoggerFactory loggerFactory, MigrationManager migrationManager, ExceptionlessElasticConfiguration configuration)
            : base(loggerFactory) {

            _migrationManager = migrationManager;
            _configuration = configuration;
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            await _configuration.ConfigureIndexesAsync(null, false).AnyContext();
            await _migrationManager.RunMigrationsAsync().AnyContext();

            var tasks = _configuration.Indexes.OfType<VersionedIndex>().Select(ReindexIfNecessary);
            await Task.WhenAll(tasks).AnyContext();

            return JobResult.Success;
        }

        private async Task ReindexIfNecessary(VersionedIndex index) {
            if (index.Version != await index.GetCurrentVersionAsync().AnyContext())
                await index.ReindexAsync().AnyContext();
        }
    }
}
