using System;
using System.Collections.Generic;
using Elasticsearch.Net;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Serialization;
using Foundatio.Caching;
using Foundatio.Elasticsearch.Configuration;
using Foundatio.Jobs;
using Foundatio.Queues;
using Nest;
using Newtonsoft.Json;

namespace Exceptionless.Core.Repositories.Configuration {
    public class ElasticConfiguration : ElasticConfigurationBase {
        public ElasticConfiguration(IQueue<WorkItemData> workItemQueue, ICacheClient cacheClient) : base(workItemQueue, cacheClient) {}

        protected override ConnectionSettings GetConnectionSettings(IEnumerable<Uri> serverUris, IEnumerable<IElasticIndex> indexes, Func<ConnectionSettings, IElasticsearchSerializer> serializerFactory = null) {
            var settings = base.GetConnectionSettings(serverUris, indexes, s => new ElasticsearchJsonNetSerializer(s))
                .DefaultTypeNameInferrer(p => p.Name.ToLowerUnderscoredWords())
                .DefaultFieldNameInferrer(p => p.ToLowerUnderscoredWords())
                .MaximumRetries(5);

            settings.SetJsonSerializerSettingsModifier(s => {
                s.ContractResolver = new EmptyCollectionElasticContractResolver(settings);
                s.AddModelConverters();
            });
            
            return settings;
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
        // Can you expose this as Connection Settings
        private readonly IConnectionSettingsValues _settings;

        public ElasticsearchJsonNetSerializer(IConnectionSettingsValues settings) : base(settings) {
            _settings = settings;
        }

        protected override void ModifyJsonSerializerSettings(JsonSerializerSettings settings) {
            // TODO What do I pass in here?
            settings.ContractResolver = new EmptyCollectionElasticContractResolver(_settings, null);
            settings.AddModelConverters();
        }
    }
}
