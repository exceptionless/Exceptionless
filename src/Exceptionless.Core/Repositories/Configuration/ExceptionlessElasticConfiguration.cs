using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Utility;
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

        public ExceptionlessElasticConfiguration(AppOptions appOptions, IQueue<WorkItemData> workItemQueue, ICacheClient cacheClient, IMessageBus messageBus, ILoggerFactory loggerFactory) : base(workItemQueue, cacheClient, messageBus, loggerFactory) {
            AppOptions = appOptions;

            _logger.LogInformation("All new indexes will be created with {ElasticsearchNumberOfShards} Shards and {ElasticsearchNumberOfReplicas} Replicas", appOptions.ParsedElasticsearchConnectionString.NumberOfShards, appOptions.ParsedElasticsearchConnectionString.NumberOfReplicas);
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

        public AppOptions AppOptions { get; }
        public StackIndex Stacks { get; }
        public EventIndex Events { get; }
        public OrganizationIndex Organizations { get; }

        private static Lazy<DateTime> _maxWaitTime = new Lazy<DateTime>(() => SystemClock.UtcNow.AddMinutes(1));
        private static bool _isFirstAttempt = true;
        protected override IElasticClient CreateElasticClient() {
            var connectionPool = CreateConnectionPool();
            var settings = new ConnectionSettings(connectionPool, s => new ElasticsearchJsonNetSerializer(s, _logger));
            ConfigureSettings(settings);
            foreach (var index in Indexes)
                index.ConfigureSettings(settings);

            var client = new ElasticClient(settings);
            var nodes = connectionPool.Nodes.Select(n => n.Uri.ToString());
            var startTime = SystemClock.UtcNow;
            if (SystemClock.UtcNow > _maxWaitTime.Value || !_isFirstAttempt)
                return client;
            
            while (!_shutdownToken.IsCancellationRequested && !client.Ping().IsValid) {
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("Waiting for Elasticsearch {Server} after {Duration:g}...", nodes, SystemClock.UtcNow.Subtract(startTime));

                if (SystemClock.UtcNow > _maxWaitTime.Value) {
                    if (_logger.IsEnabled(LogLevel.Error))
                        _logger.LogError("Unable to connect to Elasticsearch {Server} after attempting for {Duration:g}", nodes, SystemClock.UtcNow.Subtract(startTime));
                    
                    break;
                }

                Thread.Sleep(1000);
            }
            _isFirstAttempt = true;

            return client;
        }

        protected override IConnectionPool CreateConnectionPool() {
            var serverUris = AppOptions.ParsedElasticsearchConnectionString?.ServerUrl.Split(',').Select(url => new Uri(url));
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