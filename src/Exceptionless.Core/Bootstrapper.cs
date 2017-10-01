using System;
using System.Collections.Generic;
using System.Linq;
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
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Serialization;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
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
using Foundatio.Repositories.Elasticsearch.Jobs;
using Foundatio.Serializer;
using Foundatio.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Exceptionless.Core {
    public class Bootstrapper {
        public static void RegisterServices(IServiceCollection container, ILoggerFactory loggerFactory) {
            container.RegisterLogger(loggerFactory);
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings {
                DateParseHandling = DateParseHandling.DateTimeOffset
            };

            var resolver = new DynamicTypeContractResolver(new LowerCaseUnderscorePropertyNamesContractResolver());
            resolver.UseDefaultResolverFor(typeof(DataDictionary), typeof(SettingsDictionary), typeof(VersionOne.VersionOneWebHookStack), typeof(VersionOne.VersionOneWebHookEvent));

            var settings = new JsonSerializerSettings {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                DateParseHandling = DateParseHandling.DateTimeOffset,
                ContractResolver = resolver
            };

            settings.AddModelConverters(loggerFactory.CreateLogger(nameof(Bootstrapper)));

            container.AddSingleton<IContractResolver>(resolver);
            container.AddSingleton<JsonSerializerSettings>(settings);
            container.AddSingleton<JsonSerializer>(JsonSerializer.Create(settings));
            container.AddSingleton<ISerializer>(new JsonNetSerializer(settings));

            container.AddSingleton<ICacheClient>(s => new InMemoryCacheClient(new InMemoryCacheClientOptions { LoggerFactory = loggerFactory }));
            container.AddSingleton<IMetricsClient>(s => new InMemoryMetricsClient(new InMemoryMetricsClientOptions { LoggerFactory = loggerFactory }));

            container.AddSingleton<ExceptionlessElasticConfiguration>();
            if (!Settings.Current.DisableIndexConfiguration)
                container.AddStartupAction<ExceptionlessElasticConfiguration>();

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

            container.AddSingleton(s => CreateQueue<EventPost>(s, loggerFactory: loggerFactory));
            container.AddSingleton(s => CreateQueue<EventUserDescription>(s, loggerFactory: loggerFactory));
            container.AddSingleton(s => CreateQueue<EventNotificationWorkItem>(s, loggerFactory: loggerFactory));
            container.AddSingleton(s => CreateQueue<WebHookNotification>(s, loggerFactory: loggerFactory));
            container.AddSingleton(s => CreateQueue<MailMessage>(s, loggerFactory: loggerFactory));
            container.AddSingleton(s => CreateQueue<WorkItemData>(s, TimeSpan.FromHours(1), loggerFactory));

            container.AddSingleton<IMessageBus>(s => new InMemoryMessageBus(new InMemoryMessageBusOptions { LoggerFactory = loggerFactory }));
            container.AddSingleton<IMessagePublisher>(s => s.GetRequiredService<IMessageBus>());
            container.AddSingleton<IMessageSubscriber>(s => s.GetRequiredService<IMessageBus>());

            if (!String.IsNullOrEmpty(Settings.Current.StorageFolder))
                container.AddSingleton<IFileStorage>(s => new FolderFileStorage(Settings.Current.StorageFolder));
            else
                container.AddSingleton<IFileStorage>(s => new InMemoryFileStorage());

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
            container.AddSingleton(typeof(IValidator<>), typeof(Bootstrapper).Assembly);
            container.AddSingleton(typeof(IPipelineAction<EventContext>), typeof(Bootstrapper).Assembly);
            container.AddSingleton(typeof(IJob), typeof(Bootstrapper).Assembly, typeof(MailMessageJob).Assembly);

            container.AddSingleton<IMailer, Mailer>();
            container.AddSingleton<IMailSender>(s => new InMemoryMailSender());

            container.AddSingleton<ILockProvider, CacheLockProvider>();
            container.AddTransient<StripeEventHandler>();
            container.AddSingleton<BillingManager>();
            container.AddSingleton<SampleDataService>();
            container.AddSingleton<EventPipeline>();
            container.AddSingleton<EventPluginManager>();
            container.AddSingleton<FormattingPluginManager>();
            container.AddSingleton<UserAgentParser>();
            container.AddSingleton<SystemHealthChecker>();
            container.AddSingleton<ICoreLastReferenceIdManager, NullCoreLastReferenceIdManager>();

            container.AddSingleton<UsageService>();
            container.AddSingleton<SlackService>();

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

        private static IQueue<T> CreateQueue<T>(IServiceProvider container, TimeSpan? workItemTimeout = null, ILoggerFactory loggerFactory = null) where T : class {
            var behaviours = container.GetServices<IQueueBehavior<T>>().ToList();
            behaviours.Add(new MetricsQueueBehavior<T>(container.GetRequiredService<IMetricsClient>(), null, TimeSpan.FromSeconds(2), loggerFactory));

            return new InMemoryQueue<T>(new InMemoryQueueOptions<T> {
                Behaviors = behaviours,
                WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
                Serializer = container.GetRequiredService<ISerializer>(),
                LoggerFactory = loggerFactory
            });
        }
    }
}