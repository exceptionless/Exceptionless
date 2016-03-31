using System;
using System.Collections.Generic;
using Elasticsearch.Net;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Serialization;
using Foundatio.Caching;
using Foundatio.Elasticsearch.Configuration;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Queues;
using Nest;
using Newtonsoft.Json;

namespace Exceptionless.Core.Repositories.Configuration {
    public class ElasticConfiguration : ElasticConfigurationBase {
        public ElasticConfiguration(IQueue<WorkItemData> workItemQueue, ICacheClient cacheClient, ILoggerFactory loggerFactory = null) : base(workItemQueue, cacheClient, loggerFactory) {}

        protected override ConnectionSettings GetConnectionSettings(IEnumerable<Uri> serverUris, IEnumerable<IElasticIndex> indexes, Func<ConnectionSettings, IElasticsearchSerializer> serializerFactory = null) {
            return base.GetConnectionSettings(serverUris, indexes, s => new ElasticsearchJsonNetSerializer(s, _logger))
                .DefaultTypeNameInferrer(p => p.Name.ToLowerUnderscoredWords())
                .DefaultFieldNameInferrer(p => p.ToLowerUnderscoredWords())
                .MaximumRetries(5);
        }

        protected override IEnumerable<IElasticIndex> GetIndexes() {
            return new IElasticIndex[] {
                new StackIndex(),
                new EventIndex(),
                new OrganizationIndex()
            };
        }
    }

    public class ElasticsearchJsonNetSerializer : JsonNetSerializer {
        private readonly ILogger _logger;

        public ElasticsearchJsonNetSerializer(IConnectionSettingsValues settings, ILogger logger) : base(settings) {
            _logger = logger;
        }

        protected override void ModifyJsonSerializerSettings(JsonSerializerSettings settings) {
            settings.ContractResolver = new EmptyCollectionElasticContractResolver(Settings, null);
            settings.AddModelConverters(_logger);
        }
    }
}