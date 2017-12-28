using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories.Elasticsearch.Jobs;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.Elastic {
    [Job(Description = "Takes an Elasticsearch stacks index snapshot ", IsContinuous = false)]

    public class StackSnapshotJob : SnapshotJob {
        public StackSnapshotJob(ExceptionlessElasticConfiguration configuration, ILockProvider lockProvider, ILoggerFactory loggerFactory) : base(configuration.Client, lockProvider, loggerFactory) {
            Repository = Settings.Current.AppScopePrefix + "ex_stacks";
            IncludedIndexes.Add("stacks*");
        }

        public override Task<JobResult> RunAsync(CancellationToken cancellationToken = new CancellationToken()) {
            if (!Settings.Current.EnableSnapshotJobs)
                return Task.FromResult(JobResult.Success);

            return base.RunAsync(cancellationToken);
        }
    }
}