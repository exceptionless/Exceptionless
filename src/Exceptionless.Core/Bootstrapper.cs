using AutoMapper;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
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
using Exceptionless.Core.Serialization;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Serializer;
using FluentValidation;
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
using Foundatio.Repositories.Elasticsearch.Jobs;
using Foundatio.Repositories.Migrations;
using Foundatio.Serializer;
using Foundatio.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;
using MaintainIndexesJob = Foundatio.Repositories.Elasticsearch.Jobs.MaintainIndexesJob;

namespace Exceptionless.Core;

public class Bootstrapper {
    public static void RegisterServices(IServiceCollection services, AppOptions appOptions) {
        JsonConvert.DefaultSettings = () => new JsonSerializerSettings {
            DateParseHandling = DateParseHandling.DateTimeOffset
        };

        services.AddSingleton<IContractResolver>(_ => GetJsonContractResolver());
        services.AddSingleton<JsonSerializerSettings>(s => {
            // NOTE: These settings may need to be synced in the Elastic Configuration.
            var settings = new JsonSerializerSettings {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                DateParseHandling = DateParseHandling.DateTimeOffset,
                ContractResolver = s.GetRequiredService<IContractResolver>()
            };

            settings.AddModelConverters(s.GetRequiredService<ILogger<Bootstrapper>>());
            return settings;
        });

        services.AddSingleton<JsonSerializer>(s => JsonSerializer.Create(s.GetRequiredService<JsonSerializerSettings>()));
        services.AddSingleton<ISerializer>(s => new JsonNetSerializer(s.GetRequiredService<JsonSerializerSettings>()));
        services.AddSingleton<ITextSerializer>(s => new JsonNetSerializer(s.GetRequiredService<JsonSerializerSettings>()));

        services.AddSingleton<ICacheClient>(s => new InMemoryCacheClient(new InMemoryCacheClientOptions { LoggerFactory = s.GetRequiredService<ILoggerFactory>(), CloneValues = true, Serializer = s.GetRequiredService<ISerializer>() }));

        services.AddSingleton<ExceptionlessElasticConfiguration>();
        services.AddSingleton<Nest.IElasticClient>(s => s.GetRequiredService<ExceptionlessElasticConfiguration>().Client);
        services.AddSingleton<IElasticConfiguration>(s => s.GetRequiredService<ExceptionlessElasticConfiguration>());
        services.AddStartupAction<ExceptionlessElasticConfiguration>();

        services.AddStartupAction("Create Sample Data", CreateSampleDataAsync);

        services.AddSingleton(typeof(IWorkItemHandler), typeof(Bootstrapper).Assembly, typeof(ReindexWorkItemHandler).Assembly);
        services.AddSingleton<WorkItemHandlers>(s => {
            var handlers = new WorkItemHandlers();
            handlers.Register<ReindexWorkItem>(s.GetRequiredService<ReindexWorkItemHandler>);
            handlers.Register<RemoveStacksWorkItem>(s.GetRequiredService<RemoveStacksWorkItemHandler>);
            handlers.Register<RemoveBotEventsWorkItem>(s.GetRequiredService<RemoveBotEventsWorkItemHandler>);
            handlers.Register<SetLocationFromGeoWorkItem>(s.GetRequiredService<SetLocationFromGeoWorkItemHandler>);
            handlers.Register<SetProjectIsConfiguredWorkItem>(s.GetRequiredService<SetProjectIsConfiguredWorkItemHandler>);
            handlers.Register<OrganizationMaintenanceWorkItem>(s.GetRequiredService<OrganizationMaintenanceWorkItemHandler>);
            handlers.Register<OrganizationNotificationWorkItem>(s.GetRequiredService<OrganizationNotificationWorkItemHandler>);
            handlers.Register<ProjectMaintenanceWorkItem>(s.GetRequiredService<ProjectMaintenanceWorkItemHandler>);
            handlers.Register<UserMaintenanceWorkItem>(s.GetRequiredService<UserMaintenanceWorkItemHandler>);
            return handlers;
        });

        services.AddSingleton(s => CreateQueue<EventPost>(s));
        services.AddSingleton(s => CreateQueue<EventUserDescription>(s));
        services.AddSingleton(s => CreateQueue<EventNotification>(s));
        services.AddSingleton(s => CreateQueue<WebHookNotification>(s));
        services.AddSingleton(s => CreateQueue<MailMessage>(s));
        services.AddSingleton(s => CreateQueue<WorkItemData>(s, TimeSpan.FromHours(1)));

        services.AddSingleton<IConnectionMapping, ConnectionMapping>();
        services.AddSingleton<MessageService>();
        services.AddStartupAction<MessageService>();
        services.AddSingleton<IMessageBus>(s => new InMemoryMessageBus(new InMemoryMessageBusOptions { LoggerFactory = s.GetRequiredService<ILoggerFactory>(), Serializer = s.GetRequiredService<ISerializer>() }));
        services.AddSingleton<IMessagePublisher>(s => s.GetRequiredService<IMessageBus>());
        services.AddSingleton<IMessageSubscriber>(s => s.GetRequiredService<IMessageBus>());

        services.AddSingleton<IFileStorage>(s => new InMemoryFileStorage(new InMemoryFileStorageOptions {
            Serializer = s.GetRequiredService<ITextSerializer>(),
            LoggerFactory = s.GetRequiredService<ILoggerFactory>()
        }));

        services.AddSingleton(typeof(IMigration), typeof(Bootstrapper).Assembly);
        services.AddSingleton<IStackRepository, StackRepository>();
        services.AddSingleton<IEventRepository, EventRepository>();
        services.AddSingleton<IMigrationStateRepository, MigrationStateRepository>();
        services.AddSingleton<MigrationManager>();
        services.AddSingleton<MigrationIndex>(s => s.GetRequiredService<ExceptionlessElasticConfiguration>().Migrations);
        services.AddSingleton<IOrganizationRepository, OrganizationRepository>();
        services.AddSingleton<IProjectRepository, ProjectRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IWebHookRepository, WebHookRepository>();
        services.AddSingleton<ITokenRepository, TokenRepository>();

        services.AddSingleton<IGeocodeService, NullGeocodeService>();
        services.AddSingleton<IGeoIpService, NullGeoIpService>();

        services.AddSingleton<IQueryParser>(s => new ElasticQueryParser());
        services.AddSingleton<IAppQueryValidator, AppQueryValidator>();
        services.AddSingleton<PersistentEventQueryValidator>();
        services.AddSingleton<StackQueryValidator>();

        services.AddSingleton(typeof(IValidator<>), typeof(Bootstrapper).Assembly);
        services.AddSingleton(typeof(IPipelineAction<EventContext>), typeof(Bootstrapper).Assembly);
        services.AddSingleton(typeof(IPlugin), typeof(Bootstrapper).Assembly);
        services.AddSingleton(typeof(IJob), typeof(Bootstrapper).Assembly);
        services.AddSingleton<WorkItemJob>();
        services.AddSingleton<MaintainIndexesJob>();

        services.AddSingleton<IMailer, Mailer>();
        services.AddSingleton<IMailSender>(s => new InMemoryMailSender());

        services.AddSingleton<CacheLockProvider>(s => new CacheLockProvider(s.GetRequiredService<ICacheClient>(), s.GetRequiredService<IMessageBus>(), s.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<ILockProvider>(s => s.GetRequiredService<CacheLockProvider>());
        services.AddTransient<StripeEventHandler>();
        services.AddSingleton<BillingManager>();
        services.AddSingleton<BillingPlans>();
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

        services.AddSingleton<OrganizationService>();
        services.AddSingleton<UsageService>();
        services.AddSingleton<SlackService>();
        services.AddSingleton<StackService>();

        services.AddTransient<IDomainLoginProvider, ActiveDirectoryLoginProvider>();

        services.AddTransient<AutoMapper.Profile, CoreMappings>();
        services.AddSingleton<IMapper>(s => {
            var profiles = s.GetServices<AutoMapper.Profile>();
            var c = new MapperConfiguration(cfg => {
                cfg.ConstructServicesUsing(s.GetRequiredService);

                foreach (var profile in profiles)
                    cfg.AddProfile(profile);
            });

            return c.CreateMapper();
        });
    }

    public static void LogConfiguration(IServiceProvider serviceProvider, AppOptions appOptions, ILogger logger) {
        if (!logger.IsEnabled(LogLevel.Warning))
            return;

        if (String.IsNullOrEmpty(appOptions.CacheOptions.Provider))
            logger.LogWarning("Distributed cache is NOT enabled on {MachineName}.", Environment.MachineName);

        if (String.IsNullOrEmpty(appOptions.MessageBusOptions.Provider))
            logger.LogWarning("Distributed message bus is NOT enabled on {MachineName}.", Environment.MachineName);

        if (String.IsNullOrEmpty(appOptions.QueueOptions.Provider))
            logger.LogWarning("Distributed queue is NOT enabled on {MachineName}.", Environment.MachineName);

        if (String.IsNullOrEmpty(appOptions.StorageOptions.Provider))
            logger.LogWarning("Distributed storage is NOT enabled on {MachineName}.", Environment.MachineName);

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

    private static async Task CreateSampleDataAsync(IServiceProvider container) {
        var options = container.GetRequiredService<AppOptions>();
        if (!options.EnableSampleData)
            return;

        var elasticsearchOptions = container.GetRequiredService<ElasticsearchOptions>();
        if (elasticsearchOptions.DisableIndexConfiguration)
            return;

        var userRepository = container.GetRequiredService<IUserRepository>();
        if (await userRepository.CountAsync().AnyContext() != 0)
            return;

        var dataHelper = container.GetRequiredService<SampleDataService>();
        await dataHelper.CreateDataAsync().AnyContext();
    }

    public static void AddHostedJobs(IServiceCollection services, ILoggerFactory loggerFactory) {
        services.AddJob<CloseInactiveSessionsJob>(true);
        services.AddJob<DailySummaryJob>(true);
        services.AddJob<EventNotificationsJob>(true);
        services.AddJob<EventPostsJob>(true);
        services.AddJob<EventUserDescriptionsJob>(true);
        services.AddJob<MailMessageJob>(true);
        services.AddJob<StackStatusJob>(true);
        services.AddJob<StackEventCountJob>(true);
        services.AddJob<WebHooksJob>(true);
        services.AddJob<WorkItemJob>(true);

        services.AddCronJob<DownloadGeoIPDatabaseJob>("0 1 * * *");
        services.AddCronJob<CleanupDataJob>("30 */4 * * *");
        services.AddCronJob<CleanupOrphanedDataJob>("45 */8 * * *");
        services.AddCronJob<MaintainIndexesJob>("10 */2 * * *");

        var logger = loggerFactory.CreateLogger<Bootstrapper>();
        logger.LogWarning("Jobs running in process.");
    }

    public static DynamicTypeContractResolver GetJsonContractResolver() {
        var resolver = new DynamicTypeContractResolver(new LowerCaseUnderscorePropertyNamesContractResolver());
        resolver.UseDefaultResolverFor(typeof(DataDictionary), typeof(SettingsDictionary), typeof(VersionOne.VersionOneWebHookStack), typeof(VersionOne.VersionOneWebHookEvent));
        return resolver;
    }

    private static IQueue<T> CreateQueue<T>(IServiceProvider container, TimeSpan? workItemTimeout = null) where T : class {
        var loggerFactory = container.GetRequiredService<ILoggerFactory>();

        return new InMemoryQueue<T>(new InMemoryQueueOptions<T> {
            WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
            Serializer = container.GetRequiredService<ISerializer>(),
            LoggerFactory = loggerFactory
        });
    }
}
