using System;
using System.Collections.Generic;
using System.Linq;
using Elasticsearch.Net;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Serializer;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Nest;
using Newtonsoft.Json;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class ExceptionlessElasticConfiguration : ElasticConfiguration {
        public ExceptionlessElasticConfiguration(IQueue<WorkItemData> workItemQueue, ICacheClient cacheClient, IMessageBus messageBus, ILoggerFactory loggerFactory) : base(workItemQueue, cacheClient, messageBus, loggerFactory) {
            _logger.Info().Message($"All new indexes will be created with {Settings.Current.ElasticSearchNumberOfShards} Shards and {Settings.Current.ElasticSearchNumberOfReplicas} Replicas");
            AddIndex(Stacks = new StackIndex(this));
            AddIndex(Events = new EventIndex(this));
            AddIndex(Organizations = new OrganizationIndex(this));
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

        protected override IElasticClient CreateElasticClient() {
            var settings = new ConnectionSettings(CreateConnectionPool(), s => new ElasticsearchJsonNetSerializer(s, _logger));
            ConfigureSettings(settings);
            foreach (var index in Indexes)
                index.ConfigureSettings(settings);

            return new ElasticClient(settings);
        }

        protected override IConnectionPool CreateConnectionPool() {
            var serverUris = Settings.Current.ElasticSearchConnectionString.Split(',').Select(url => new Uri(url));
            return new StaticConnectionPool(serverUris);
        }

        protected override void ConfigureSettings(ConnectionSettings settings) {
            settings.DisableDirectStreaming()
                .EnableTcpKeepAlive(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2))
                .DefaultTypeNameInferrer(p => p.Name.ToLowerUnderscoredWords())
                .DefaultFieldNameInferrer(p => p.ToLowerUnderscoredWords())
                .MaximumRetries(5);
        }
    }

    public class ElasticsearchJsonNetSerializer : JsonNetSerializer {
        public ElasticsearchJsonNetSerializer(IConnectionSettingsValues settings, ILogger logger)
            : base(settings, (serializerSettings, values) => {
                var resolver = new ElasticDynamicTypeContractResolver(values, new List<Func<Type, JsonConverter>>());
                serializerSettings.ContractResolver = resolver;
                serializerSettings.AddModelConverters(logger);
            }) {
        }
    }
}