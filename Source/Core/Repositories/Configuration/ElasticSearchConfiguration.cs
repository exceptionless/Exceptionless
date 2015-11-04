using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Serialization;
using Foundatio.Caching;
using Foundatio.Elasticsearch.Configuration;
using Foundatio.Jobs;
using Foundatio.Queues;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class ElasticsearchConfiguration : ElasticsearchConfigurationBase {
        public ElasticsearchConfiguration(IQueue<WorkItemData> workItemQueue, ICacheClient cacheClient) : base(workItemQueue, cacheClient) {}
        
        protected override ConnectionSettings GetConnectionSettings(IEnumerable<Uri> serverUris, IEnumerable<IElasticsearchIndex> indexes) {
            var settings = base.GetConnectionSettings(serverUris, indexes)
                .SetDefaultTypeNameInferrer(p => p.Name.ToLowerUnderscoredWords())
                .SetDefaultPropertyNameInferrer(p => p.ToLowerUnderscoredWords())
                .MaximumRetries(5);

            settings.SetJsonSerializerSettingsModifier(s => {
                s.ContractResolver = new EmptyCollectionElasticContractResolver(settings);
                s.AddModelConverters();
            });

            return settings;
        }
        
        protected override IEnumerable<IElasticsearchIndex> GetIndexes() {
            return new IElasticsearchIndex[] {
                new StackIndex(),
                new EventIndex(),
                new OrganizationIndex()
            };
        }
    }
}