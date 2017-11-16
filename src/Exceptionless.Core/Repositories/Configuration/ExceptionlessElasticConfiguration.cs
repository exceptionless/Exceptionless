using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Serializer;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Nest;
using Newtonsoft.Json;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

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
            var connectionPool = CreateConnectionPool();
            var settings = new ConnectionSettings(connectionPool, s => new ElasticsearchJsonNetSerializer(s, _logger));
            ConfigureSettings(settings);
            foreach (var index in Indexes)
                index.ConfigureSettings(settings);

            var client = new ElasticClient(settings);
            var nodes = connectionPool.Nodes.Select(n => n.Uri.ToString());
            var startTime = SystemClock.UtcNow;
            var maxWaitTime = TimeSpan.FromMinutes(1);
            while (!_shutdownToken.IsCancellationRequested && !client.Ping().IsValid) {
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("Waiting for Elasticsearch {Server} after {Duration:g}...", nodes, SystemClock.UtcNow.Subtract(startTime));

                Thread.Sleep(1000);
                if (SystemClock.UtcNow.Subtract(startTime) > maxWaitTime) {
                    if (_logger.IsEnabled(LogLevel.Error))
                        _logger.LogError("Unable to connect to Elasticsearch {Server} after attempting for {Duration:g}", nodes, SystemClock.UtcNow.Subtract(startTime));
                    break;
                }
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