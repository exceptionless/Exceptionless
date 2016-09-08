using System;
using System.Linq;
using Elasticsearch.Net.ConnectionPool;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Serialization;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class ExceptionlessElasticConfiguration : ElasticConfiguration {
        public ExceptionlessElasticConfiguration(IQueue<WorkItemData> workItemQueue, ICacheClient cacheClient, IMessageBus messageBus, ILoggerFactory loggerFactory) : base(workItemQueue, cacheClient, messageBus, loggerFactory) {
            _logger.Info().Message($"All new indexes will be created with {Settings.Current.ElasticSearchNumberOfShards} Shards and {Settings.Current.ElasticSearchNumberOfReplicas} Replicas");
            AddIndex(Stacks = new StackIndex(this));
            AddIndex(Events = new EventIndex(this));
            AddIndex(Organizations = new OrganizationIndex(this));
            
            // TODO: REMOVE THIS
            ConfigureIndexesAsync(beginReindexingOutdated: false).GetAwaiter().GetResult();
        }
        
        public override void ConfigureGlobalQueryBuilders(ElasticQueryBuilder builder) {
            builder.Register(new ExceptionlessSystemFilterQueryBuilder());
            builder.Register(new OrganizationIdQueryBuilder());
            builder.Register(new ProjectIdQueryBuilder());
            builder.Register(new StackIdQueryBuilder());
        }

        public StackIndex Stacks { get; }
        public EventIndex Events { get; }
        public OrganizationIndex Organizations { get; }

        protected override IConnectionPool CreateConnectionPool() {
            var serverUris = Settings.Current.ElasticSearchConnectionString.Split(',').Select(url => new Uri(url));
            return new StaticConnectionPool(serverUris);
        }

        protected override void ConfigureSettings(ConnectionSettings settings) {
            settings
                .EnableTcpKeepAlive(30 * 1000, 2000)
                .SetDefaultTypeNameInferrer(p => p.Name.ToLowerUnderscoredWords())
                .SetDefaultPropertyNameInferrer(p => p.ToLowerUnderscoredWords())
                .MaximumRetries(5);

            settings.SetJsonSerializerSettingsModifier(s => {
                s.ContractResolver = new EmptyCollectionElasticContractResolver(settings);
                s.AddModelConverters(_logger);
            });
        }
    }
}
