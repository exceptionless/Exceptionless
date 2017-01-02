using System;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Lock;
using Foundatio.Logging;


namespace Exceptionless.Core.Jobs.Elastic {
    public class CleanupSnapshotJob : Foundatio.Repositories.Elasticsearch.Jobs.CleanupSnapshotJob {
        public CleanupSnapshotJob(ExceptionlessElasticConfiguration configuration, ILockProvider lockProvider, ILoggerFactory loggerFactory)
            : base(configuration.Client, lockProvider, loggerFactory) {
            AddRepository(Settings.Current.AppScopePrefix + "ex_organizations", TimeSpan.FromDays(7));
            AddRepository(Settings.Current.AppScopePrefix + "ex_stacks", TimeSpan.FromDays(7));
            AddRepository(Settings.Current.AppScopePrefix + "ex_events", TimeSpan.FromDays(7));
        }
    }
}