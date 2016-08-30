using System;
using System.Linq;
using Elasticsearch.Net.ConnectionPool;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Serialization;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Queues;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class ExceptionlessElasticConfiguration : ElasticConfiguration {
        public ExceptionlessElasticConfiguration(IQueue<WorkItemData> workItemQueue, ICacheClient cacheClient, ILoggerFactory loggerFactory) : base(null, workItemQueue, cacheClient, loggerFactory) {
            Client = new ElasticClient(GetConnectionSettings());

            // register our custom app query builders
            ElasticQueryBuilder.Default.RegisterDefaults();
            ElasticQueryBuilder.Default.Register(new ExceptionlessSystemFilterQueryBuilder());
            ElasticQueryBuilder.Default.Register(new OrganizationIdQueryBuilder());
            ElasticQueryBuilder.Default.Register(new ProjectIdQueryBuilder());
            ElasticQueryBuilder.Default.Register(new StackIdQueryBuilder());

            _logger.Info().Message($"All new indexes will be created with {Settings.Current.ElasticSearchNumberOfShards} Shards and {Settings.Current.ElasticSearchNumberOfReplicas} Replicas");
            Stacks = new StackIndex(Client, cacheClient, loggerFactory);
            Events = new EventIndex(Client, cacheClient, loggerFactory);
            Organizations = new OrganizationIndex(Client, cacheClient, loggerFactory);

            AddIndex(Stacks);
            AddIndex(Events);
            AddIndex(Organizations);
        }

        public StackIndex Stacks { get; }
        public EventIndex Events { get; }
        public OrganizationIndex Organizations { get; }

        private ConnectionSettings GetConnectionSettings() {
            var serverUris = Settings.Current.ElasticSearchConnectionString.Split(',').Select(url => new Uri(url));
            var settings = new ConnectionSettings(new StaticConnectionPool(serverUris)).EnableTcpKeepAlive(30 * 1000, 2000)
                .SetDefaultTypeNameInferrer(p => p.Name.ToLowerUnderscoredWords())
                .SetDefaultPropertyNameInferrer(p => p.ToLowerUnderscoredWords())
                .MaximumRetries(5);

            settings.SetJsonSerializerSettingsModifier(s => {
                s.ContractResolver = new EmptyCollectionElasticContractResolver(settings);
                s.AddModelConverters(_logger);
            });

            return settings;
        }
    }
}
