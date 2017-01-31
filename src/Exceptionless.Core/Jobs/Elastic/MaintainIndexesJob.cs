using System;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Lock;
using Foundatio.Logging;

namespace Exceptionless.Core.Jobs.Elastic {
    public class MaintainIndexesJob : Foundatio.Repositories.Elasticsearch.Jobs.MaintainIndexesJob {
        public MaintainIndexesJob(ExceptionlessElasticConfiguration configuration, ILockProvider lockProvider, ILoggerFactory loggerFactory) : base(configuration, lockProvider, loggerFactory) {}
    }
}