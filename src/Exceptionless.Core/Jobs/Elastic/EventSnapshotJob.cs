using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories.Elasticsearch.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Jobs.Elastic {
    [Job(Description = "Takes an Elasticsearch events index snapshot ", IsContinuous = false)]
    public class EventSnapshotJob : SnapshotJob {
        private readonly IOptionsSnapshot<AppOptions> _options;

        public EventSnapshotJob(ExceptionlessElasticConfiguration configuration, ILockProvider lockProvider, IOptionsSnapshot<AppOptions> options, ILoggerFactory loggerFactory) : base(configuration.Client, lockProvider, loggerFactory) {
            _options = options;
            Repository = _options.Value.ScopePrefix + "ex_events";
            IncludedIndexes.Add("events*");
        }

        public override Task<JobResult> RunAsync(CancellationToken cancellationToken = new CancellationToken()) {
            if (!_options.Value.EnableSnapshotJobs)
                return Task.FromResult(JobResult.Success);

            return base.RunAsync(cancellationToken);
        }
    }
}