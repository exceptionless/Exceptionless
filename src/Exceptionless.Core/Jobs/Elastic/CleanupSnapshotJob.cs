using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Jobs;
using Foundatio.Lock;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Jobs.Elastic {
    [Job(Description = "Removes old Elasticsearch snapshots.", IsContinuous = false)]
    public class CleanupSnapshotJob : Foundatio.Repositories.Elasticsearch.Jobs.CleanupSnapshotJob {
        private readonly IOptionsSnapshot<AppOptions> _options;

        public CleanupSnapshotJob(ExceptionlessElasticConfiguration configuration, ILockProvider lockProvider, IOptionsSnapshot<AppOptions> options, ILoggerFactory loggerFactory)
            : base(configuration.Client, lockProvider, loggerFactory) {
            _options = options;
            AddRepository(_options.Value.ScopePrefix + "ex_organizations", TimeSpan.FromDays(7));
            AddRepository(_options.Value.ScopePrefix + "ex_stacks", TimeSpan.FromDays(7));
            AddRepository(_options.Value.ScopePrefix + "ex_events", TimeSpan.FromDays(7));
        }

        public override Task<JobResult> RunAsync(CancellationToken cancellationToken = new CancellationToken()) {
            if (!_options.Value.EnableSnapshotJobs)
                return Task.FromResult(JobResult.Success);

            return base.RunAsync(cancellationToken);
        }
    }
}