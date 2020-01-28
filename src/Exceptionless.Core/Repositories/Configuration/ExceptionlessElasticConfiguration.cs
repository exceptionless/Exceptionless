using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Serialization;
using Foundatio.Caching;
using Foundatio.Hosting.Startup;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories.Elasticsearch;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Newtonsoft.Json;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class ExceptionlessElasticConfiguration : ElasticConfiguration, IStartupAction {
        private readonly IOptions<ElasticsearchOptions> _options;
        private readonly IOptions<AppOptions> _appOptions;
        private readonly JsonSerializerSettings _serializerSettings;

        public ExceptionlessElasticConfiguration(
            IOptions<ElasticsearchOptions> options, 
            IOptions<AppOptions> appOptions, 
            IQueue<WorkItemData> workItemQueue, 
            JsonSerializerSettings serializerSettings,
            ICacheClient cacheClient, 
            IMessageBus messageBus, 
            ILoggerFactory loggerFactory
        ) : base(workItemQueue, cacheClient, messageBus, loggerFactory) {
            _options = options;
            _appOptions = appOptions;
            _serializerSettings = serializerSettings;

            _logger.LogInformation("All new indexes will be created with {ElasticsearchNumberOfShards} Shards and {ElasticsearchNumberOfReplicas} Replicas", options.Value.NumberOfShards, options.Value.NumberOfReplicas);
            AddIndex(Stacks = new StackIndex(this));
            AddIndex(Events = new EventIndex(this, appOptions));
            AddIndex(Migrations = new MigrationIndex(this, options.Value.ScopePrefix + "migrations", appOptions.Value.AppMode == AppMode.Development ? 0 : 1));
            AddIndex(Organizations = new OrganizationIndex(this));
            AddIndex(Projects = new ProjectIndex(this));
            AddIndex(Tokens = new TokenIndex(this));
            AddIndex(Users = new UserIndex(this));
            AddIndex(WebHooks = new WebHookIndex(this));
        }

        public Task RunAsync(CancellationToken shutdownToken = default) {
            if (_options.Value.DisableIndexConfiguration)
                return Task.CompletedTask;

            return ConfigureIndexesAsync();
        }

        public override void ConfigureGlobalQueryBuilders(ElasticQueryBuilder builder) {
            builder.Register(new AppFilterQueryBuilder(_appOptions));
            builder.Register(new OrganizationQueryBuilder());
            builder.Register(new ProjectQueryBuilder());
            builder.Register(new StackQueryBuilder());
        }

        public ElasticsearchOptions Options => _options.Value;
        public StackIndex Stacks { get; }
        public EventIndex Events { get; }
        public MigrationIndex Migrations { get; }
        public OrganizationIndex Organizations { get; }
        public ProjectIndex Projects { get; }
        public TokenIndex Tokens { get; }
        public UserIndex Users { get; }
        public WebHookIndex WebHooks { get; }

        protected override IElasticClient CreateElasticClient() {
            var connectionPool = CreateConnectionPool();
            var settings = new ConnectionSettings(connectionPool, (serializer, values) => new ElasticJsonNetSerializer(serializer, values, _serializerSettings));

            ConfigureSettings(settings);
            foreach (var index in Indexes)
                index.ConfigureSettings(settings);
                
            if (!String.IsNullOrEmpty(_options.Value.UserName) && !String.IsNullOrEmpty(_options.Value.Password))
                settings.BasicAuthentication(_options.Value.UserName, _options.Value.Password);
                
            var client = new ElasticClient(settings);
            return client;
        }

        protected override IConnectionPool CreateConnectionPool() {
            var serverUris = Options?.ServerUrl.Split(',').Select(url => new Uri(url));
            return new StaticConnectionPool(serverUris);
        }

        protected override void ConfigureSettings(ConnectionSettings settings) {
            if (_appOptions.Value.AppMode == AppMode.Development)
                settings.DisableDirectStreaming().PrettyJson();
            
            settings.EnableTcpKeepAlive(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2))
                .DefaultFieldNameInferrer(p => p.ToLowerUnderscoredWords())
                .MaximumRetries(5);
        }
    }
}