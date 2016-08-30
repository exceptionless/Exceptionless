using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Serialization;
using Foundatio.Caching;
using Foundatio.Elasticsearch.Configuration;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Queues;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class ElasticConfiguration : ElasticConfigurationBase {
        private readonly ILogger _logger;

        public ElasticConfiguration(IQueue<WorkItemData> workItemQueue, ICacheClient cacheClient, ILogger<ElasticConfiguration> logger) : base(workItemQueue, cacheClient) {
            _logger = logger;
            _logger.Info().Message($"All new indexes will be created with {Settings.Current.ElasticSearchNumberOfShards} Shards and {Settings.Current.ElasticSearchNumberOfReplicas} Replicas");
        }

        protected override ConnectionSettings GetConnectionSettings(IEnumerable<Uri> serverUris, IEnumerable<IElasticIndex> indexes) {
            var settings = base.GetConnectionSettings(serverUris, indexes)
                .SetDefaultTypeNameInferrer(p => p.Name.ToLowerUnderscoredWords())
                .SetDefaultPropertyNameInferrer(p => p.ToLowerUnderscoredWords())
                .MaximumRetries(5);

            settings.SetJsonSerializerSettingsModifier(s => {
                s.ContractResolver = new EmptyCollectionElasticContractResolver(settings);
                s.AddModelConverters(_logger);
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
}
