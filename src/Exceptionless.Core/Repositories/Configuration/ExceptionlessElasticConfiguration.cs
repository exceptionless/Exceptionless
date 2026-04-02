using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Serialization;
using Elastic.Transport;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Serialization;
using Foundatio.Caching;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories.Elasticsearch;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Serialization;
using Foundatio.Resilience;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class ExceptionlessElasticConfiguration : ElasticConfiguration, IStartupAction
{
    private readonly AppOptions _appOptions;

    public ExceptionlessElasticConfiguration(
        AppOptions appOptions,
        IQueue<WorkItemData> workItemQueue,
        ICacheClient cacheClient,
        IMessageBus messageBus,
        IServiceProvider serviceProvider,
        ITextSerializer serializer,
        TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider,
        ILoggerFactory loggerFactory
    ) : base(workItemQueue, cacheClient, messageBus, serializer, timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        _appOptions = appOptions;

        _logger.LogInformation("All new indexes will be created with {ElasticsearchNumberOfShards} Shards and {ElasticsearchNumberOfReplicas} Replicas", _appOptions.ElasticsearchOptions.NumberOfShards, _appOptions.ElasticsearchOptions.NumberOfReplicas);
        AddIndex(Stacks = new StackIndex(this));
        AddIndex(Events = new EventIndex(this, serviceProvider, appOptions));
        AddIndex(Migrations = new MigrationIndex(this, _appOptions.ElasticsearchOptions.ScopePrefix + "migrations", appOptions.ElasticsearchOptions.NumberOfReplicas));
        AddIndex(Organizations = new OrganizationIndex(this));
        AddIndex(Projects = new ProjectIndex(this));
        AddIndex(Tokens = new TokenIndex(this));
        AddIndex(Users = new UserIndex(this));
        AddIndex(WebHooks = new WebHookIndex(this));
    }

    public Task RunAsync(CancellationToken shutdownToken = default)
    {
        if (_appOptions.ElasticsearchOptions.DisableIndexConfiguration)
            return Task.CompletedTask;

        return ConfigureIndexesAsync();
    }

    public override void ConfigureGlobalQueryBuilders(ElasticQueryBuilder builder)
    {
        builder.Register(new AppFilterQueryBuilder(_appOptions, TimeProvider));
        builder.Register(new OrganizationQueryBuilder());
        builder.Register(new ProjectQueryBuilder());
        builder.Register(new StackQueryBuilder());
    }

    public ElasticsearchOptions Options => _appOptions.ElasticsearchOptions;
    public StackIndex Stacks { get; }
    public EventIndex Events { get; }
    public MigrationIndex Migrations { get; }
    public OrganizationIndex Organizations { get; }
    public ProjectIndex Projects { get; }
    public TokenIndex Tokens { get; }
    public UserIndex Users { get; }
    public WebHookIndex WebHooks { get; }

    protected override ElasticsearchClient CreateElasticClient()
    {
        var connectionPool = CreateConnectionPool();

        // Settings are intentionally not disposed: they're owned by the ElasticsearchClient for the
        // app's lifetime. The configuration is registered as a singleton in DI, so both the settings
        // and client live until process exit.
        var settings = new ElasticsearchClientSettings(
            connectionPool,
            sourceSerializer: (_, clientSettings) =>
                new DefaultSourceSerializer(clientSettings, options =>
                {
                    // Base defaults from DI + Foundatio
                    options.ConfigureExceptionlessDefaults();
                    options.ConfigureFoundatioRepositoryDefaults();

                    // ES-specific overrides (legacy data compatibility)
                    options.RespectNullableAnnotations = false;

                    // ES needs all integers as long to match the old JSON.NET DataObjectConverter behavior.
                    // Remove existing ObjectToInferredTypesConverter instances (from both Configure calls)
                    // and insert preferInt64: true version at position 0 so STJ picks it first.
                    for (int i = options.Converters.Count - 1; i >= 0; i--)
                    {
                        if (options.Converters[i] is Exceptionless.Core.Serialization.ObjectToInferredTypesConverter)
                            options.Converters.RemoveAt(i);
                    }
                    options.Converters.Insert(0, new Exceptionless.Core.Serialization.ObjectToInferredTypesConverter(preferInt64: true));
                }));

        ConfigureSettings(settings);
        foreach (var index in Indexes)
            index.ConfigureSettings(settings);

        if (!String.IsNullOrEmpty(_appOptions.ElasticsearchOptions.UserName) && !String.IsNullOrEmpty(_appOptions.ElasticsearchOptions.Password))
            settings.Authentication(new BasicAuthentication(_appOptions.ElasticsearchOptions.UserName, _appOptions.ElasticsearchOptions.Password));

        var client = new ElasticsearchClient(settings);
        return client;
    }

    protected override NodePool CreateConnectionPool()
    {
        var serverUris = Options.ServerUrl?.Split(',').Select(url => new Uri(url))
            ?? throw new InvalidOperationException("ElasticsearchOptions.ServerUrl is not configured.");
        return new StaticNodePool(serverUris);
    }

    protected override void ConfigureSettings(ElasticsearchClientSettings settings)
    {
        if (_appOptions.AppMode == AppMode.Development)
            settings.EnableDebugMode();

        settings.ServerCertificateValidationCallback((_, _, _, _) => true);
        settings.DisableDirectStreaming();
        settings.EnableTcpKeepAlive(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2));
        settings.DefaultFieldNameInferrer(p => p.ToLowerUnderscoredWords());
        settings.MaximumRetries(5);
    }
}
