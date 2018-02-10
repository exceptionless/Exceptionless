using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.EquivalencyExpression;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Billing;
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
using Exceptionless.DateTimeExtensions;
using Exceptionless.Serializer;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Foundatio.Parsers.ElasticQueries;
using Foundatio.Parsers.LuceneQueries;
using Foundatio.Queues;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Jobs;
using Foundatio.Serializer;
using Foundatio.Storage;
using Foundatio.Utility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;

namespace Exceptionless.Core {
    public class Bootstrapper {
        public static void RegisterServices(IServiceCollection container) {
            container.AddSingleton<IServiceCollection>(container);
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings {
                DateParseHandling = DateParseHandling.DateTimeOffset
            };

            container.AddSingleton<IContractResolver>(s => GetJsonContractResolver());
            container.AddSingleton<JsonSerializerSettings>(s => {
                var settings = new JsonSerializerSettings {
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    DateParseHandling = DateParseHandling.DateTimeOffset,
                    ContractResolver = s.GetRequiredService<IContractResolver>()
                };

                settings.AddModelConverters(s.GetRequiredService<ILogger<Bootstrapper>>());
                return settings;
            });

            container.AddSingleton<JsonSerializer>(s => JsonSerializer.Create(s.GetRequiredService<JsonSerializerSettings>()));
            container.AddSingleton<ISerializer>(s => new JsonNetSerializer(s.GetRequiredService<JsonSerializerSettings>()));
            container.AddSingleton<ITextSerializer>(s => new JsonNetSerializer(s.GetRequiredService<JsonSerializerSettings>()));

            container.AddSingleton<ICacheClient>(s => new InMemoryCacheClient(new InMemoryCacheClientOptions { LoggerFactory = s.GetRequiredService<ILoggerFactory>() }));
            container.AddSingleton<IMetricsClient>(s => new InMemoryMetricsClient(new InMemoryMetricsClientOptions { LoggerFactory = s.GetRequiredService<ILoggerFactory>() }));

            container.AddSingleton<ExceptionlessElasticConfiguration>();
            container.AddSingleton<IElasticConfiguration>(s => s.GetRequiredService<ExceptionlessElasticConfiguration>());
            if (!Settings.Current.DisableIndexConfiguration)
                container.AddStartupAction<ExceptionlessElasticConfiguration>();

            container.AddStartupAction(CreateSampleDataAsync);

            container.AddSingleton<IQueueBehavior<EventPost>>(s => new MetricsQueueBehavior<EventPost>(s.GetRequiredService<IMetricsClient>()));
            container.AddSingleton<IQueueBehavior<EventUserDescription>>(s => new MetricsQueueBehavior<EventUserDescription>(s.GetRequiredService<IMetricsClient>()));
            container.AddSingleton<IQueueBehavior<EventNotificationWorkItem>>(s => new MetricsQueueBehavior<EventNotificationWorkItem>(s.GetRequiredService<IMetricsClient>()));
            container.AddSingleton<IQueueBehavior<WebHookNotification>>(s => new MetricsQueueBehavior<WebHookNotification>(s.GetRequiredService<IMetricsClient>()));
            container.AddSingleton<IQueueBehavior<MailMessage>>(s => new MetricsQueueBehavior<MailMessage>(s.GetRequiredService<IMetricsClient>()));
            container.AddSingleton<IQueueBehavior<WorkItemData>>(s => new MetricsQueueBehavior<WorkItemData>(s.GetRequiredService<IMetricsClient>()));

            container.AddSingleton(typeof(IWorkItemHandler), typeof(Bootstrapper).Assembly);
            container.AddSingleton<WorkItemHandlers>(s => {
                var handlers = new WorkItemHandlers();
                handlers.Register<ReindexWorkItem>(s.GetRequiredService<ReindexWorkItemHandler>);
                handlers.Register<RemoveOrganizationWorkItem>(s.GetRequiredService<RemoveOrganizationWorkItemHandler>);
                handlers.Register<RemoveProjectWorkItem>(s.GetRequiredService<RemoveProjectWorkItemHandler>);
                handlers.Register<SetLocationFromGeoWorkItem>(s.GetRequiredService<SetLocationFromGeoWorkItemHandler>);
                handlers.Register<SetProjectIsConfiguredWorkItem>(s.GetRequiredService<SetProjectIsConfiguredWorkItemHandler>);
                handlers.Register<StackWorkItem>(s.GetRequiredService<StackWorkItemHandler>);
                handlers.Register<ThrottleBotsWorkItem>(s.GetRequiredService<ThrottleBotsWorkItemHandler>);
                handlers.Register<OrganizationMaintenanceWorkItem>(s.GetRequiredService<OrganizationMaintenanceWorkItemHandler>);
                handlers.Register<OrganizationNotificationWorkItem>(s.GetRequiredService<OrganizationNotificationWorkItemHandler>);
                handlers.Register<ProjectMaintenanceWorkItem>(s.GetRequiredService<ProjectMaintenanceWorkItemHandler>);
                handlers.Register<UserMaintenanceWorkItem>(s.GetRequiredService<UserMaintenanceWorkItemHandler>);
                return handlers;
            });

            container.AddSingleton(s => CreateQueue<EventPost>(s));
            container.AddSingleton(s => CreateQueue<EventUserDescription>(s));
            container.AddSingleton(s => CreateQueue<EventNotificationWorkItem>(s));
            container.AddSingleton(s => CreateQueue<WebHookNotification>(s));
            container.AddSingleton(s => CreateQueue<MailMessage>(s));
            container.AddSingleton(s => CreateQueue<WorkItemData>(s, TimeSpan.FromHours(1)));

            container.AddSingleton<IConnectionMapping, ConnectionMapping>();
            container.AddSingleton<MessageService>();
            container.AddStartupAction<MessageService>();
            container.AddSingleton<IMessageBus>(s => new InMemoryMessageBus(new InMemoryMessageBusOptions { LoggerFactory = s.GetRequiredService<ILoggerFactory>() }));
            container.AddSingleton<IMessagePublisher>(s => s.GetRequiredService<IMessageBus>());
            container.AddSingleton<IMessageSubscriber>(s => s.GetRequiredService<IMessageBus>());

            if (!String.IsNullOrEmpty(Settings.Current.StorageFolder)) {
                container.AddSingleton<IFileStorage>(s => new FolderFileStorage(new FolderFileStorageOptions {
                    Folder = Settings.Current.StorageFolder,
                    Serializer = s.GetRequiredService<ITextSerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            } else {
                container.AddSingleton<IFileStorage>(s => new InMemoryFileStorage(new InMemoryFileStorageOptions {
                    Serializer = s.GetRequiredService<ITextSerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            }

            container.AddSingleton<IStackRepository, StackRepository>();
            container.AddSingleton<IEventRepository, EventRepository>();
            container.AddSingleton<IOrganizationRepository, OrganizationRepository>();
            container.AddSingleton<IProjectRepository, ProjectRepository>();
            container.AddSingleton<IUserRepository, UserRepository>();
            container.AddSingleton<IWebHookRepository, WebHookRepository>();
            container.AddSingleton<ITokenRepository, TokenRepository>();

            container.AddSingleton<IGeoIpService, MaxMindGeoIpService>();
            container.AddSingleton<IGeocodeService, NullGeocodeService>();

            container.AddSingleton<IQueryParser>(s => new ElasticQueryParser());
            container.AddSingleton<IQueryValidator, QueryValidator>();
            container.AddSingleton<PersistentEventQueryValidator>();
            container.AddSingleton<StackQueryValidator>();

            container.AddSingleton(typeof(IValidator<>), typeof(Bootstrapper).Assembly);
            container.AddSingleton(typeof(IPipelineAction<EventContext>), typeof(Bootstrapper).Assembly);
            container.AddSingleton(typeof(IPlugin), typeof(Bootstrapper).Assembly);
            container.AddSingleton<EventParserPluginManager>();
            container.AddSingleton<EventPluginManager>();
            container.AddSingleton<EventUpgraderPluginManager>();
            container.AddSingleton<FormattingPluginManager>();
            container.AddSingleton<WebHookDataPluginManager>();
            container.AddSingleton(typeof(IJob), typeof(Bootstrapper).Assembly);
            container.AddSingleton<WorkItemJob>();
            container.AddSingleton<MaintainIndexesJob>();

            container.AddSingleton<IMailer, Mailer>();
            container.AddSingleton<IMailSender>(s => new InMemoryMailSender());

            container.AddSingleton<CacheLockProvider>(s => new CacheLockProvider(s.GetRequiredService<ICacheClient>(), s.GetRequiredService<IMessageBus>(), s.GetRequiredService<ILoggerFactory>()));
            container.AddSingleton<ILockProvider>(s => s.GetRequiredService<CacheLockProvider>());
            container.AddTransient<StripeEventHandler>();
            container.AddSingleton<BillingManager>();
            container.AddSingleton<EventPostService>();
            container.AddSingleton<SampleDataService>();
            container.AddSingleton<SemanticVersionParser>();
            container.AddSingleton<EventParserPluginManager>();
            container.AddSingleton<EventPipeline>();
            container.AddSingleton<EventPluginManager>();
            container.AddSingleton<FormattingPluginManager>();
            container.AddSingleton<WebHookDataPluginManager>();
            container.AddSingleton<UserAgentParser>();
            container.AddSingleton<SystemHealthChecker>();
            container.AddSingleton<ICoreLastReferenceIdManager, NullCoreLastReferenceIdManager>();

            container.AddSingleton<UsageService>();
            container.AddSingleton<SlackService>();
            container.AddSingleton<StackService>();

            container.AddTransient<IDomainLoginProvider, ActiveDirectoryLoginProvider>();

            container.AddTransient<AutoMapper.Profile, CoreMappings>();
            container.AddSingleton<IMapper>(s => {
                var profiles = s.GetServices<AutoMapper.Profile>();
                var config = new MapperConfiguration(cfg => {
                    cfg.AddCollectionMappers();
                    cfg.ConstructServicesUsing(s.GetRequiredService);

                    foreach (var profile in profiles)
                        cfg.AddProfile(profile);
                });

                return config.CreateMapper();
            });
        }

        public static void LogConfiguration(IServiceProvider serviceProvider, ILoggerFactory loggerFactory) {
            var logger = loggerFactory.CreateLogger<Bootstrapper>();
            if (!logger.IsEnabled(LogLevel.Warning))
                return;

            if (!Settings.Current.EnableMetricsReporting)
                logger.LogWarning("StatsD Metrics is NOT enabled on {MachineName}.", Environment.MachineName);

            if (String.IsNullOrEmpty(Settings.Current.RedisConnectionString))
                logger.LogWarning("Redis is NOT enabled on {MachineName}.", Environment.MachineName);

            if (!Settings.Current.EnableWebSockets)
                logger.LogWarning("Web Sockets is NOT enabled on {MachineName}", Environment.MachineName);

            if (Settings.Current.AppMode == AppMode.Development)
                logger.LogWarning("Emails will NOT be sent in Development mode on {MachineName}", Environment.MachineName);

            if (String.IsNullOrEmpty(Settings.Current.AzureStorageConnectionString))
                logger.LogWarning("Azure Storage is NOT enabled on {MachineName}", Environment.MachineName);

            var fileStorage = serviceProvider.GetRequiredService<IFileStorage>();
            if (fileStorage is InMemoryFileStorage)
                logger.LogWarning("Using in memory file storage on {MachineName}", Environment.MachineName);

            if (!Settings.Current.EnableBootstrapStartupActions)
                logger.LogWarning("Startup Actions is NOT enabled on {MachineName}", Environment.MachineName);

            if (Settings.Current.DisableIndexConfiguration)
                logger.LogWarning("Index Configuration is NOT enabled on {MachineName}", Environment.MachineName);

            if (Settings.Current.EventSubmissionDisabled)
                logger.LogWarning("Event Submission is NOT enabled on {MachineName}", Environment.MachineName);

            if (!Settings.Current.EnableAccountCreation)
                logger.LogWarning("Account Creation is NOT enabled on {MachineName}", Environment.MachineName);
        }

        private static async Task CreateSampleDataAsync(IServiceProvider container) {
            if (Settings.Current.AppMode != AppMode.Development)
                return;

            var userRepository = container.GetRequiredService<IUserRepository>();
            if (await userRepository.CountAsync().AnyContext() != 0)
                return;

            var dataHelper = container.GetRequiredService<SampleDataService>();
            await dataHelper.CreateDataAsync().AnyContext();
        }

        public static void RunJobs(IServiceProvider container, ILoggerFactory loggerFactory, CancellationToken token) {
            var logger = loggerFactory.CreateLogger("AppBuilder");
            if (!Settings.Current.RunJobsInProcess) {
                logger.LogInformation("Jobs running out of process.");
                return;
            }

            new JobRunner(container.GetRequiredService<EventPostsJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(2)).RunInBackground(token);
            new JobRunner(container.GetRequiredService<EventUserDescriptionsJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(3)).RunInBackground(token);
            new JobRunner(container.GetRequiredService<EventNotificationsJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(5)).RunInBackground(token);
            new JobRunner(container.GetRequiredService<MailMessageJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(5)).RunInBackground(token);
            new JobRunner(container.GetRequiredService<WebHooksJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(5)).RunInBackground(token);
            new JobRunner(container.GetRequiredService<CloseInactiveSessionsJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(30), interval: TimeSpan.FromSeconds(30)).RunInBackground(token);
            new JobRunner(container.GetRequiredService<DailySummaryJob>(), loggerFactory, initialDelay: TimeSpan.FromMinutes(1), interval: TimeSpan.FromHours(1)).RunInBackground(token);
            new JobRunner(container.GetRequiredService<DownloadGeoIPDatabaseJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(5), interval: TimeSpan.FromDays(1)).RunInBackground(token);
            new JobRunner(container.GetRequiredService<RetentionLimitsJob>(), loggerFactory, initialDelay: TimeSpan.FromMinutes(15), interval: TimeSpan.FromHours(1)).RunInBackground(token);
            new JobRunner(container.GetRequiredService<WorkItemJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(2), instanceCount: 2).RunInBackground(token);
            new JobRunner(container.GetRequiredService<MaintainIndexesJob>(), loggerFactory, initialDelay: SystemClock.UtcNow.Ceiling(TimeSpan.FromHours(1)) - SystemClock.UtcNow, interval: TimeSpan.FromHours(1)).RunInBackground(token);
            new JobRunner(container.GetRequiredService<StackEventCountJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(2), interval: TimeSpan.FromSeconds(5)).RunInBackground(token);

            logger.LogWarning("Jobs running in process.");
        }

        public static DynamicTypeContractResolver GetJsonContractResolver() {
            var resolver = new DynamicTypeContractResolver(new LowerCaseUnderscorePropertyNamesContractResolver());
            resolver.UseDefaultResolverFor(typeof(DataDictionary), typeof(SettingsDictionary), typeof(VersionOne.VersionOneWebHookStack), typeof(VersionOne.VersionOneWebHookEvent));
            return resolver;
        }

        private static IQueue<T> CreateQueue<T>(IServiceProvider container, TimeSpan? workItemTimeout = null) where T : class {
            var loggerFactory = container.GetRequiredService<ILoggerFactory>();

            var behaviors = container.GetServices<IQueueBehavior<T>>().ToList();
            behaviors.Add(new MetricsQueueBehavior<T>(container.GetRequiredService<IMetricsClient>(), null, TimeSpan.FromSeconds(2), loggerFactory));

            return new InMemoryQueue<T>(new InMemoryQueueOptions<T> {
                Behaviors = behaviors,
                WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
                Serializer = container.GetRequiredService<ISerializer>(),
                LoggerFactory = loggerFactory
            });
        }
    }
}