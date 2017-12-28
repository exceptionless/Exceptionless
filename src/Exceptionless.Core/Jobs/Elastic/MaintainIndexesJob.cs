using System;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Jobs;
using Foundatio.Lock;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.Elastic {
    [Job(Description = "Maintains Elasticsearch index aliases and index retention", IsContinuous = false)]
    public class MaintainIndexesJob : Foundatio.Repositories.Elasticsearch.Jobs.MaintainIndexesJob {
        public MaintainIndexesJob(ExceptionlessElasticConfiguration configuration, ILockProvider lockProvider, ILoggerFactory loggerFactory) : base(configuration, lockProvider, loggerFactory) {}
    }
}