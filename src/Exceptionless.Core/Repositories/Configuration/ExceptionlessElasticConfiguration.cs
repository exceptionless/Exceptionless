using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Serializer;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Microsoft.Extensions.Logging;
using Nest;
using Newtonsoft.Json;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class ExceptionlessElasticConfiguration : ElasticConfiguration, IStartupAction {
        private CancellationToken _shutdownToken;

        public ExceptionlessElasticConfiguration(IQueue<WorkItemData> workItemQueue, ICacheClient cacheClient, IMessageBus messageBus, ILoggerFactory loggerFactory) : base(workItemQueue, cacheClient, messageBus, loggerFactory) {
            _logger.LogInformation("All new indexes will be created with {ElasticSearchNumberOfShards} Shards and {ElasticSearchNumberOfReplicas} Replicas", Settings.Current.ElasticSearchNumberOfShards, Settings.Current.ElasticSearchNumberOfReplicas);
            AddIndex(Stacks = new StackIndex(this));
            AddIndex(Events = new EventIndex(this));
            AddIndex(Organizations = new OrganizationIndex(this));
        }

        public Task RunAsync(CancellationToken shutdownToken = default) {
            _shutdownToken = shutdownToken;
            return ConfigureIndexesAsync();
        }

        public override void ConfigureGlobalQueryBuilders(ElasticQueryBuilder builder) {
            builder.Register(new ExceptionlessSystemFilterQueryBuilder());
            builder.Register(new OrganizationQueryBuilder());
            builder.Register(new ProjectQueryBuilder());
            builder.Register(new StackQueryBuilder());
        }

        public StackIndex Stacks { get; }
        public EventIndex Events { get; }
        public OrganizationIndex Organizations { get; }

        protected override IElasticClient CreateElasticClient() {
            var settings = new ConnectionSettings(CreateConnectionPool(), s => new ElasticsearchJsonNetSerializer(s, _logger));
            ConfigureSettings(settings);
            foreach (var index in Indexes)
                index.ConfigureSettings(settings);

            var client = new ElasticClient(settings);
            var startTime = DateTime.Now;
            while (!_shutdownToken.IsCancellationRequested && !client.Ping().IsValid) {
                _logger.LogWarning("Waiting for Elasticsearch ({Duration})...", DateTime.Now.Subtract(startTime).ToWords(true));
                Thread.Sleep(1000);
                if (DateTime.Now.Subtract(startTime) > TimeSpan.FromMinutes(2))
                    break;
            }

            return client;
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