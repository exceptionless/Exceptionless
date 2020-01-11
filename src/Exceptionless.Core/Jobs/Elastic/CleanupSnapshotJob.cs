using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Jobs;
using Foundatio.Lock;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.Elastic {
    [Job(Description = "Removes old Elasticsearch snapshots.", IsContinuous = false)]
    public class CleanupSnapshotJob : Foundatio.Repositories.Elasticsearch.Jobs.CleanupSnapshotJob {
        private readonly ExceptionlessElasticConfiguration _configuration;

        public CleanupSnapshotJob(ExceptionlessElasticConfiguration configuration, ILockProvider lockProvider, ILoggerFactory loggerFactory)
            : base(configuration.Client, lockProvider, loggerFactory) {
            _configuration = configuration;
            AddRepository(configuration.Options.ScopePrefix + "organizations", TimeSpan.FromDays(7));
            AddRepository(configuration.Options.ScopePrefix + "stacks", TimeSpan.FromDays(7));
            AddRepository(configuration.Options.ScopePrefix + "events", TimeSpan.FromDays(7));
        }

        public override Task<JobResult> RunAsync(CancellationToken cancellationToken = new CancellationToken()) {
            if (!_configuration.Options.EnableSnapshotJobs)
                return Task.FromResult(JobResult.Success);

            return base.RunAsync(cancellationToken);
        }
    }
}