using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.EventUpgrader;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Seed;
using Exceptionless.Core.Serialization;
using Exceptionless.Core.Services;
using Exceptionless.Core.Services.SourceMaps;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Validation;
using Foundatio.Caching;
using Foundatio.Extensions.Hosting.Jobs;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Parsers.ElasticQueries;
using Foundatio.Parsers.LuceneQueries;
using Foundatio.Queues;
using Foundatio.Repositories.Elasticsearch;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.CustomFields;
using Foundatio.Repositories.Elasticsearch.Jobs;
using Foundatio.Repositories.Migrations;
using Foundatio.Resilience;
using Foundatio.Serializer;
using Foundatio.Storage;
using Foundatio.Utility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MaintainIndexesJob = Foundatio.Repositories.Elasticsearch.Jobs.MaintainIndexesJob;
using MigrationJob = Exceptionless.Core.Jobs.Elastic.MigrationJob;

namespace Exceptionless.Core;

public class Bootstrapper
{
    public static void RegisterServices(IServiceCollection services, AppOptions appOptions, bool runDataSeedStartupAction = true)
    {
        // Register System.Text.Json options with Exceptionless defaults (snake_case, null handling)
        services.AddSingleton(_ => new JsonSerializerOptions().ConfigureExceptionlessDefaults());

        services.AddSingleton<ISerializer>(s => s.GetRequiredService<ITextSerializer>());
        services.AddSingleton<ITextSerializer>(s => new SystemTextJsonSerializer(s.GetRequiredService<JsonSerializerOptions>()));

        services.ReplaceSingleton<TimeProvider>(_ => TimeProvider.System);
        services.AddSingleton<IResiliencePolicyProvider, ResiliencePolicyProvider>();
        services.AddSingleton<ICacheClient>(s => new InMemoryCacheClient(new InMemoryCacheClientOptions
        {
            CloneValues = true,
            Serializer = s.GetRequiredService<ISerializer>(),
            TimeProvider = s.GetRequiredService<TimeProvider>(),
            ResiliencePolicyProvider = s.GetRequiredService<IResiliencePolicyProvider>(),
            LoggerFactory = s.GetRequiredService<ILoggerFactory>()
        }));

        services.AddSingleton<ExceptionlessElasticConfiguration>();
        services.AddSingleton<ElasticsearchClient>(s => s.GetRequiredService<ExceptionlessElasticConfiguration>().Client);
        services.AddSingleton<IElasticConfiguration>(s => s.GetRequiredService<ExceptionlessElasticConfiguration>());
        services.AddSingleton<ICustomFieldDefinitionRepository>(s => s.GetRequiredService<ExceptionlessElasticConfiguration>().CustomFieldDefinitionRepository!);
        services.AddStartupAction<ExceptionlessElasticConfiguration>();

        services.AddSingleton<DataSeedService>();
        services.AddSingleton<IDataSeed, PredefinedSavedViewsDataSeed>();
        if (runDataSeedStartupAction)
            services.AddStartupAction<DataSeedService>();

        services.AddStartupAction("Create Sample Data", CreateSampleDataAsync);

        services.AddSingleton(typeof(IWorkItemHandler), typeof(Bootstrapper).Assembly, typeof(ReindexWorkItemHandler).Assembly);
        services.AddSingleton<WorkItemHandlers>(s =>
        {
            var handlers = new WorkItemHandlers();
            handlers.Register<FixStackStatsWorkItem>(s.GetRequiredService<FixStackStatsWorkItemHandler>);
            handlers.Register<ForcePredefinedSavedViewsWorkItem>(s.GetRequiredService<ForcePredefinedSavedViewsWorkItemHandler>);
            handlers.Register<OrganizationMaintenanceWorkItem>(s.GetRequiredService<OrganizationMaintenanceWorkItemHandler>);
            handlers.Register<OrganizationNotificationWorkItem>(s.GetRequiredService<OrganizationNotificationWorkItemHandler>);
            handlers.Register<ProjectMaintenanceWorkItem>(s.GetRequiredService<ProjectMaintenanceWorkItemHandler>);
            handlers.Register<ReindexWorkItem>(s.GetRequiredService<ReindexWorkItemHandler>);
            handlers.Register<RemoveBotEventsWorkItem>(s.GetRequiredService<RemoveBotEventsWorkItemHandler>);
            handlers.Register<RemoveStacksWorkItem>(s.GetRequiredService<RemoveStacksWorkItemHandler>);
            handlers.Register<ResetProjectDataWorkItem>(s.GetRequiredService<ResetProjectDataWorkItemHandler>);
            handlers.Register<SetLocationFromGeoWorkItem>(s.GetRequiredService<SetLocationFromGeoWorkItemHandler>);
            handlers.Register<SetProjectIsConfiguredWorkItem>(s.GetRequiredService<SetProjectIsConfiguredWorkItemHandler>);
            handlers.Register<UpdateProjectNotificationSettingsWorkItem>(s.GetRequiredService<UpdateProjectNotificationSettingsWorkItemHandler>);
            handlers.Register<UserMaintenanceWorkItem>(s.GetRequiredService<UserMaintenanceWorkItemHandler>);
            handlers.Register<GenerateSampleEventsWorkItem>(s.GetRequiredService<GenerateSampleEventsWorkItemHandler>);
            return handlers;
        });

        services.AddSingleton(s => CreateQueue<EventPost>(s));
        services.AddSingleton(s => CreateQueue<EventUserDescription>(s));
        services.AddSingleton(s => CreateQueue<EventNotification>(s));
        services.AddSingleton(s => CreateQueue<WebHookNotification>(s));
        services.AddSingleton(s => CreateQueue<MailMessage>(s));
        services.AddSingleton(s => CreateQueue<WorkItemData>(s, TimeSpan.FromHours(1)));

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IQueueBehavior<WorkItemData>, WorkItemDuplicateDetectionQueueBehavior>());

        services.AddSingleton<IConnectionMapping, ConnectionMapping>();
        services.AddSingleton<MessageService>();
        services.AddStartupAction<MessageService>();
        services.AddSingleton<IMessageBus>(s => new InMemoryMessageBus(new InMemoryMessageBusOptions
        {
            Serializer = s.GetRequiredService<ISerializer>(),
            TimeProvider = s.GetRequiredService<TimeProvider>(),
            ResiliencePolicyProvider = s.GetRequiredService<IResiliencePolicyProvider>(),
            LoggerFactory = s.GetRequiredService<ILoggerFactory>()
        }));
        services.AddSingleton<IMessagePublisher>(s => s.GetRequiredService<IMessageBus>());
        services.AddSingleton<IMessageSubscriber>(s => s.GetRequiredService<IMessageBus>());

        services.AddSingleton<IFileStorage>(s => new InMemoryFileStorage(new InMemoryFileStorageOptions
        {
            Serializer = s.GetRequiredService<ITextSerializer>(),
            TimeProvider = s.GetRequiredService<TimeProvider>(),
            ResiliencePolicyProvider = s.GetRequiredService<IResiliencePolicyProvider>(),
            LoggerFactory = s.GetRequiredService<ILoggerFactory>()
        }));

        services.AddSingleton(typeof(IMigration), typeof(Bootstrapper).Assembly);
        services.AddSingleton<IStackRepository, StackRepository>();
        services.AddSingleton<IEventRepository, EventRepository>();
        services.AddSingleton<IMigrationStateRepository, MigrationStateRepository>();
        services.AddSingleton<MigrationManager>();
        services.AddSingleton<MigrationIndex>(s => s.GetRequiredService<ExceptionlessElasticConfiguration>().Migrations);
        services.AddSingleton<IOrganizationRepository, OrganizationRepository>();
        services.AddSingleton<IOAuthApplicationRepository, OAuthApplicationRepository>();
        services.AddSingleton<IOAuthTokenRepository, OAuthTokenRepository>();
        services.AddSingleton<IProjectRepository, ProjectRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IWebHookRepository, WebHookRepository>();
        services.AddSingleton<ISavedViewRepository, SavedViewRepository>();
        services.AddSingleton<ITokenRepository, TokenRepository>();

        services.AddSingleton<IGeocodeService, NullGeocodeService>();
        services.AddSingleton<IGeoIpService, NullGeoIpService>();

        services.AddSingleton<IQueryParser>(s => new ElasticQueryParser());
        services.AddSingleton<IAppQueryValidator, AppQueryValidator>();
        services.AddSingleton<PersistentEventQueryValidator>();
        services.AddSingleton<EventStackQueryValidator>();
        services.AddSingleton<StackQueryValidator>();

        services.AddSingleton<MiniValidationValidator>();
        services.AddSingleton(typeof(IPipelineAction<EventContext>), typeof(Bootstrapper).Assembly);
        services.AddSingleton(typeof(IPlugin), typeof(Bootstrapper).Assembly);
        services.AddSingleton(typeof(IJob), typeof(Bootstrapper).Assembly);
        services.AddSingleton<WorkItemJob>();
        services.AddSingleton<MaintainIndexesJob>();

        services.AddSingleton<IMailer, Mailer>();
        services.AddSingleton<IMailSender>(s => new InMemoryMailSender());

        services.AddSingleton<CacheLockProvider>(s => new CacheLockProvider(s.GetRequiredService<ICacheClient>(), s.GetRequiredService<IMessageBus>(), s.GetRequiredService<TimeProvider>(), s.GetRequiredService<IResiliencePolicyProvider>(), s.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<ILockProvider>(s => s.GetRequiredService<CacheLockProvider>());
        services.AddTransient<StripeEventHandler>();
        services.AddSingleton<IStripeBillingClient, StripeBillingClient>();
        services.AddSingleton<BillingManager>();
        services.AddSingleton<BillingPlans>();
        services.AddSingleton<EventCustomFieldQueryPolicy>();
        services.AddSingleton<EventCustomFieldService>();
        services.AddStartupAction<EventCustomFieldService>();
        services.AddSingleton<EventPostService>();
        services.AddSingleton<SampleDataService>();
        services.AddSingleton<SemanticVersionParser>();
        services.AddSingleton<EventParserPluginManager>();
        services.AddSingleton<EventPipeline>();
        services.AddSingleton<EventPluginManager>();
        services.AddSingleton<EventUpgraderPluginManager>();
        services.AddSingleton<FormattingPluginManager>();
        services.AddSingleton<WebHookDataPluginManager>();
        services.AddSingleton<UserAgentParser>();
        services.AddSingleton<ICoreLastReferenceIdManager, NullCoreLastReferenceIdManager>();

        services.AddSingleton<NotificationService>();
        services.AddSingleton<OrganizationService>();
        services.AddStartupAction<OrganizationService>();
        services.AddHttpClient<IOAuthClientMetadataService, OAuthClientMetadataService>()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectCallback = ConnectToPublicAddressAsync
            });
        services.AddSingleton<SourceMapRequestThrottle>();
        services.AddHttpClient(SourceMapService.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(serviceProvider => CreateSourceMapHttpMessageHandler(
                serviceProvider.GetRequiredService<SourceMapRequestThrottle>(),
                DecompressionMethods.All));
        services.AddHttpClient(SourceMapService.GeneratedFileHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(serviceProvider => CreateSourceMapHttpMessageHandler(
                serviceProvider.GetRequiredService<SourceMapRequestThrottle>(),
                DecompressionMethods.None));
        services.AddSingleton<SourceMapService>();
        services.AddSingleton<OAuthService>();
        services.AddSingleton<UsageService>();
        services.AddSingleton<SlackService>();
        services.AddSingleton<StackService>();

        services.AddTransient<IDomainLoginProvider, ActiveDirectoryLoginProvider>();
    }

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        Exception? lastException = null;
        foreach (var address in addresses)
        {
            if (!OAuthClientMetadataService.IsPublicAddress(address))
                continue;

            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException ex)
            {
                lastException = ex;
                socket.Dispose();
            }
        }

        throw new HttpRequestException($"Host '{context.DnsEndPoint.Host}' did not resolve to a reachable public address.", lastException);
    }

    internal static SocketsHttpHandler CreateSourceMapHttpMessageHandler(SourceMapRequestThrottle throttle, DecompressionMethods automaticDecompression)
        => new()
        {
            ActivityHeadersPropagator = null,
            AllowAutoRedirect = false,
            AutomaticDecompression = automaticDecompression,
            ConnectCallback = throttle.ConnectToPublicAddressAsync,
            PreAuthenticate = false,
            UseCookies = false,
            UseProxy = false
        };

    public static void LogConfiguration(IServiceProvider serviceProvider, AppOptions appOptions, ILogger logger)
    {
        if (logger.IsEnabled(LogLevel.Information))
            LogConfigurationSummary(serviceProvider, appOptions, logger);

        if (!logger.IsEnabled(LogLevel.Warning))
            return;

        if (String.IsNullOrEmpty(appOptions.CacheOptions.Provider))
            logger.LogWarning("Distributed cache is NOT enabled on {MachineName}", Environment.MachineName);

        if (String.IsNullOrEmpty(appOptions.MessageBusOptions.Provider))
            logger.LogWarning("Distributed message bus is NOT enabled on {MachineName}", Environment.MachineName);

        if (String.IsNullOrEmpty(appOptions.QueueOptions.Provider))
            logger.LogWarning("Distributed queue is NOT enabled on {MachineName}", Environment.MachineName);

        if (String.IsNullOrEmpty(appOptions.StorageOptions.Provider))
            logger.LogWarning("Distributed storage is NOT enabled on {MachineName}", Environment.MachineName);

        if (!appOptions.EnableWebSockets)
            logger.LogWarning("Web Sockets is NOT enabled on {MachineName}", Environment.MachineName);

        if (String.IsNullOrEmpty(appOptions.EmailOptions.SmtpHost))
            logger.LogWarning("Emails will NOT be sent until the SmtpHost is configured on {MachineName}", Environment.MachineName);

        var fileStorage = serviceProvider.GetService<IFileStorage>();
        if (fileStorage is InMemoryFileStorage)
            logger.LogWarning("Using in memory file storage on {MachineName}", Environment.MachineName);

        if (appOptions.ElasticsearchOptions.DisableIndexConfiguration)
            logger.LogWarning("Index Configuration is NOT enabled on {MachineName}", Environment.MachineName);

        if (appOptions.EventSubmissionDisabled)
            logger.LogWarning("Event Submission is NOT enabled on {MachineName}", Environment.MachineName);

        if (!appOptions.AuthOptions.EnableAccountCreation)
            logger.LogWarning("Account Creation is NOT enabled on {MachineName}", Environment.MachineName);
    }

    private static void LogConfigurationSummary(IServiceProvider serviceProvider, AppOptions options, ILogger logger)
    {
        string environmentName = serviceProvider.GetService<IHostEnvironment>()?.EnvironmentName ?? options.AppMode.ToString();
        string version = options.InformationalVersion ?? options.Version ?? "unknown";

        logger.LogInformation(
            "Startup configuration: environment {EnvironmentName}, scope {AppScope}, mode {AppMode}, version {Version}, base URL {BaseUrl}",
            environmentName,
            options.AppScope,
            options.AppMode,
            version,
            GetSafeEndpoint(options.BaseURL));

        logger.LogInformation(
            "Startup infrastructure: Elasticsearch at {ElasticsearchEndpoint}; cache {CacheProvider} at {CacheEndpoint}; message bus {MessageBusProvider} at {MessageBusEndpoint}; queue {QueueProvider} at {QueueEndpoint}; storage {StorageProvider} at {StorageEndpoint}",
            GetSafeEndpoint(options.ElasticsearchOptions.ServerUrl),
            GetProvider(options.CacheOptions.Provider),
            GetProviderEndpoint(options.CacheOptions.Data, options.CacheOptions.ConnectionString),
            GetProvider(options.MessageBusOptions.Provider),
            GetProviderEndpoint(options.MessageBusOptions.Data, options.MessageBusOptions.ConnectionString),
            GetProvider(options.QueueOptions.Provider),
            GetProviderEndpoint(options.QueueOptions.Data, options.QueueOptions.ConnectionString),
            GetProvider(options.StorageOptions.Provider),
            GetProviderEndpoint(options.StorageOptions.Data, options.StorageOptions.ConnectionString));

        logger.LogInformation(
            "Startup services: event submission {EventSubmission}; WebSockets {WebSockets}; jobs in process {JobsInProcess}; email {Email}; account creation {AccountCreation}; index configuration {IndexConfiguration}",
            GetStatus(!options.EventSubmissionDisabled),
            GetStatus(options.EnableWebSockets),
            GetStatus(options.RunJobsInProcess),
            GetStatus(!String.IsNullOrWhiteSpace(options.EmailOptions.SmtpHost)),
            GetStatus(options.AuthOptions.EnableAccountCreation),
            GetStatus(!options.ElasticsearchOptions.DisableIndexConfiguration));

        logger.LogInformation(
            "Startup optional integrations/auth providers: {EnabledIntegrations}",
            GetEnabledIntegrations(options));
    }

    private static string GetProvider(string? provider)
        => String.IsNullOrWhiteSpace(provider) ? "disabled" : provider;

    private static string GetStatus(bool enabled) => enabled ? "enabled" : "disabled";

    private static string GetEnabledIntegrations(AppOptions options)
    {
        List<string> integrations = [];
        if (!String.IsNullOrWhiteSpace(options.AuthOptions.GoogleId))
            integrations.Add("Google OAuth");
        if (!String.IsNullOrWhiteSpace(options.AuthOptions.MicrosoftId))
            integrations.Add("Microsoft OAuth");
        if (!String.IsNullOrWhiteSpace(options.AuthOptions.FacebookId))
            integrations.Add("Facebook OAuth");
        if (!String.IsNullOrWhiteSpace(options.AuthOptions.GitHubId))
            integrations.Add("GitHub OAuth");
        if (options.AuthOptions.EnableActiveDirectoryAuth)
            integrations.Add("Active Directory");
        if (options.IntercomOptions.EnableIntercom)
            integrations.Add("Intercom");
        if (options.SlackOptions.EnableSlack)
            integrations.Add("Slack");
        if (options.StripeOptions.EnableBilling)
            integrations.Add("billing");
        if (!String.IsNullOrWhiteSpace(options.GoogleGeocodingApiKey))
            integrations.Add("geocoding");
        if (!String.IsNullOrWhiteSpace(options.MaxMindGeoIpKey))
            integrations.Add("GeoIP");
        if (!String.IsNullOrWhiteSpace(options.ExceptionlessApiKey))
            integrations.Add("internal Exceptionless logging");

        return integrations.Count > 0 ? String.Join(", ", integrations) : "none";
    }

    private static string GetProviderEndpoint(
        IDictionary<string, string?>? data,
        string? connectionString)
    {
        string? endpoint = GetConnectionSetting(
            data,
            "server",
            "serviceurl",
            "endpoint",
            "host",
            "hostname",
            "blobendpoint",
            "queueendpoint");
        if (endpoint is not null)
            return GetSafeEndpoint(endpoint);

        if (String.IsNullOrWhiteSpace(connectionString))
            return "not configured";

        try
        {
            var parsed = connectionString.ParseConnectionString(defaultKey: "server");
            endpoint = GetConnectionSetting(
                parsed,
                "server",
                "serviceurl",
                "endpoint",
                "host",
                "hostname",
                "blobendpoint",
                "queueendpoint");
            if (endpoint is not null)
                return GetSafeEndpoint(endpoint);
        }
        catch (ArgumentException) { }

        return GetSafeEndpoint(connectionString);
    }

    private static string? GetConnectionSetting(
        IDictionary<string, string?>? data,
        params string[] keys)
    {
        if (data is null)
            return null;

        foreach (string key in keys)
        {
            if (data.TryGetValue(key, out string? value) && !String.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string GetSafeEndpoint(string? value)
    {
        if (String.IsNullOrWhiteSpace(value))
            return "not configured";

        string candidate = value.Trim().Trim('"', '\'');
        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            int separatorIndex = candidate.IndexOfAny([',', ';']);
            if (separatorIndex >= 0)
                candidate = candidate[..separatorIndex].Trim();
        }

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) && !String.IsNullOrWhiteSpace(uri.Host))
            return GetUriEndpoint(uri);

        if (!candidate.Contains('=') && Uri.TryCreate($"tcp://{candidate}", UriKind.Absolute, out uri) && !String.IsNullOrWhiteSpace(uri.Host))
            return GetHostAndPort(uri.Host, uri.IsDefaultPort ? 0 : uri.Port);

        return "configured";
    }

    private static string GetUriEndpoint(Uri uri)
    {
        string host = uri.HostNameType == UriHostNameType.IPv6 ? $"[{uri.Host}]" : uri.IdnHost;
        return uri.IsDefaultPort ? $"{uri.Scheme}://{host}" : $"{uri.Scheme}://{host}:{uri.Port}";
    }

    private static string GetHostAndPort(string? host, int port)
    {
        if (String.IsNullOrWhiteSpace(host))
            return "not configured";

        return port > 0 ? $"{host}:{port}" : host;
    }

    private static async Task CreateSampleDataAsync(IServiceProvider container)
    {
        var options = container.GetRequiredService<AppOptions>();
        if (!options.EnableSampleData)
            return;

        var elasticsearchOptions = container.GetRequiredService<ElasticsearchOptions>();
        if (elasticsearchOptions.DisableIndexConfiguration)
            return;

        var userRepository = container.GetRequiredService<IUserRepository>();
        if (await userRepository.CountAsync() != 0)
            return;

        var dataHelper = container.GetRequiredService<SampleDataService>();
        await dataHelper.CreateDataAsync();
        await dataHelper.EnqueueSampleEventsAsync();
    }

    public static void AddHostedJobs(IServiceCollection services, ILoggerFactory loggerFactory)
    {
        services.AddJob<CloseInactiveSessionsJob>(o => o.WaitForStartupActions());
        services.AddJob<DailySummaryJob>(o => o.WaitForStartupActions());
        services.AddJob<EventNotificationsJob>(o => o.WaitForStartupActions());
        services.AddJob<EventPostsJob>(o => o.WaitForStartupActions());
        services.AddJob<EventUserDescriptionsJob>(o => o.WaitForStartupActions());
        services.AddJob<MailMessageJob>(o => o.WaitForStartupActions());
        services.AddJob<MigrationJob>(o => o.WaitForStartupActions());
        services.AddJob<StackStatusJob>(o => o.WaitForStartupActions());
        services.AddJob<StackEventCountJob>(o => o.WaitForStartupActions());
        services.AddJob<WebHooksJob>(o => o.WaitForStartupActions());
        services.AddJob<WorkItemJob>(o => o.WaitForStartupActions());

        services.AddDistributedCronJob<EventUsageJob>(Cron.Minutely());
        services.AddDistributedCronJob<CleanupDataJob>("30 */4 * * *");
        services.AddDistributedCronJob<CleanupOrphanedDataJob>("45 */8 * * *");
        services.AddDistributedCronJob<DownloadGeoIPDatabaseJob>(Cron.Daily(1));
        services.AddDistributedCronJob<MaintainIndexesJob>("10 */2 * * *");

        var logger = loggerFactory.CreateLogger<Bootstrapper>();
        logger.LogWarning("Jobs running in process");
    }

    private static IQueue<T> CreateQueue<T>(IServiceProvider container, TimeSpan? workItemTimeout = null) where T : class
    {
        var loggerFactory = container.GetRequiredService<ILoggerFactory>();

        return new InMemoryQueue<T>(new InMemoryQueueOptions<T>
        {
            WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
            Behaviors = container.GetServices<IQueueBehavior<T>>().ToList(),
            Serializer = container.GetRequiredService<ISerializer>(),
            TimeProvider = container.GetRequiredService<TimeProvider>(),
            ResiliencePolicyProvider = container.GetRequiredService<IResiliencePolicyProvider>(),
            LoggerFactory = loggerFactory
        });
    }

    private sealed class WorkItemDuplicateDetectionQueueBehavior(ICacheClient cacheClient, ILoggerFactory loggerFactory)
        : DuplicateDetectionQueueBehavior<WorkItemData>(cacheClient, loggerFactory, TimeSpan.FromHours(24));
}
