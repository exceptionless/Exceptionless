using System;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Lock;
using Foundatio.Logging;
using Foundatio.Repositories.Elasticsearch.Jobs;

namespace Exceptionless.Core.Jobs.Elastic {
    public class OrganizationSnapshotJob : SnapshotJob {
        public OrganizationSnapshotJob(ExceptionlessElasticConfiguration configuration, ILockProvider lockProvider, ILoggerFactory loggerFactory) : base(configuration.Client, lockProvider, loggerFactory) {
            Repository = Settings.Current.AppScopePrefix + "ex_organizations";
            IncludedIndexes.Add("organizations*");
        }
    }
}