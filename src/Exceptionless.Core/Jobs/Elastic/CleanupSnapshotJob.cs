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
        public CleanupSnapshotJob(ExceptionlessElasticConfiguration configuration, ILockProvider lockProvider, ILoggerFactory loggerFactory)
            : base(configuration.Client, lockProvider, loggerFactory) {
            AddRepository(Settings.Current.AppScopePrefix + "ex_organizations", TimeSpan.FromDays(7));
            AddRepository(Settings.Current.AppScopePrefix + "ex_stacks", TimeSpan.FromDays(7));
            AddRepository(Settings.Current.AppScopePrefix + "ex_events", TimeSpan.FromDays(7));
        }

        public override Task<JobResult> RunAsync(CancellationToken cancellationToken = new CancellationToken()) {
            if (!Settings.Current.EnableSnapshotJobs)
                return Task.FromResult(JobResult.Success);

            return base.RunAsync(cancellationToken);
        }
    }
}