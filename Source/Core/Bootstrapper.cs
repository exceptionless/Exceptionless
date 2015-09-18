using System;
using System.Linq;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Validation;
using Exceptionless.Serializer;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Serializer;
using Foundatio.ServiceProviders;
using Foundatio.Storage;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RazorSharpEmail;
using SimpleInjector;
using SimpleInjector.Packaging;

namespace Exceptionless.Core {
    public class Bootstrapper : IPackage {
        public void RegisterServices(Container container) {
            // Foundation service provider
            ServiceProvider.Current = container;
            container.RegisterSingleton<IDependencyResolver>(() => new SimpleInjectorCoreDependencyResolver(container));

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings {
                DateParseHandling = DateParseHandling.DateTimeOffset
            };

            var contractResolver = new ExceptionlessContractResolver();
            contractResolver.UseDefaultResolverFor(typeof(DataDictionary), typeof(SettingsDictionary), typeof(VersionOne.VersionOneWebHookStack), typeof(VersionOne.VersionOneWebHookEvent));

            var settings = new JsonSerializerSettings {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                DateParseHandling = DateParseHandling.DateTimeOffset,
                ContractResolver = contractResolver
            };
            settings.AddModelConverters();
            
            container.RegisterSingleton<IContractResolver>(() => contractResolver);
            container.RegisterSingleton<JsonSerializerSettings>(settings);
            container.RegisterSingleton<JsonSerializer>(JsonSerializer.Create(settings));
            container.RegisterSingleton<ISerializer>(() => new JsonNetSerializer(settings));

            var metricsClient = new InMemoryMetricsClient();
            metricsClient.StartDisplayingStats();
            container.RegisterSingleton<IMetricsClient>(metricsClient);

            container.RegisterSingleton<IElasticClient>(async () => await container.GetInstance<ElasticSearchConfiguration>().GetClientAsync(Settings.Current.ElasticSearchConnectionString.Split(',').Select(url => new Uri(url))).AnyContext());
            container.RegisterSingleton<ICacheClient, InMemoryCacheClient>();

            container.RegisterSingleton<IQueue<EventPost>>(() => new InMemoryQueue<EventPost>(statName: MetricNames.PostsQueueSize, metrics: container.GetInstance<IMetricsClient>()));
            container.RegisterSingleton<IQueue<EventUserDescription>>(() => new InMemoryQueue<EventUserDescription>(statName: MetricNames.EventsUserDescriptionQueueSize, metrics: container.GetInstance<IMetricsClient>()));
            container.RegisterSingleton<IQueue<EventNotificationWorkItem>>(() => new InMemoryQueue<EventNotificationWorkItem>(statName: MetricNames.EventNotificationQueueSize, metrics: container.GetInstance<IMetricsClient>()));
            container.RegisterSingleton<IQueue<WebHookNotification>>(() => new InMemoryQueue<WebHookNotification>(statName: MetricNames.WebHookQueueSize, metrics: container.GetInstance<IMetricsClient>()));
            container.RegisterSingleton<IQueue<MailMessage>>(() => new InMemoryQueue<MailMessage>(statName: MetricNames.EmailsQueueSize, metrics: container.GetInstance<IMetricsClient>()));
            container.RegisterSingleton<IQueue<StatusMessage>>(() => new InMemoryQueue<StatusMessage>());

            var workItemHandlers = new WorkItemHandlers();
            workItemHandlers.Register<ReindexWorkItem, ReindexWorkItemHandler>();
            container.RegisterSingleton<WorkItemHandlers>(workItemHandlers);
            container.RegisterSingleton<IQueue<WorkItemData>>(() => new InMemoryQueue<WorkItemData>(statName: MetricNames.WorkItemQueueSize, metrics: container.GetInstance<IMetricsClient>(), workItemTimeout: TimeSpan.FromHours(1)));
            

            container.RegisterSingleton<IMessageBus, InMemoryMessageBus>();
            container.RegisterSingleton<IMessagePublisher>(container.GetInstance<IMessageBus>);
            container.RegisterSingleton<IMessageSubscriber>(container.GetInstance<IMessageBus>);

            if (!String.IsNullOrEmpty(Settings.Current.StorageFolder))
                container.RegisterSingleton<IFileStorage>(new FolderFileStorage(Settings.Current.StorageFolder));
            else
                container.RegisterSingleton<IFileStorage>(new InMemoryFileStorage());

            container.RegisterSingleton<IStackRepository, StackRepository>();
            container.RegisterSingleton<IEventRepository, EventRepository>();
            container.RegisterSingleton<IOrganizationRepository, OrganizationRepository>();
            container.RegisterSingleton<IProjectRepository, ProjectRepository>();
            container.RegisterSingleton<IUserRepository, UserRepository>();
            container.RegisterSingleton<IWebHookRepository, WebHookRepository>();
            container.RegisterSingleton<ITokenRepository, TokenRepository>();
            container.RegisterSingleton<IApplicationRepository, ApplicationRepository>();

            container.RegisterSingleton<IGeoIPResolver, MindMaxGeoIPResolver>();

            container.RegisterSingleton<IValidator<Application>, ApplicationValidator>();
            container.RegisterSingleton<IValidator<Organization>, OrganizationValidator>();
            container.RegisterSingleton<IValidator<PersistentEvent>, PersistentEventValidator>();
            container.RegisterSingleton<IValidator<Project>, ProjectValidator>();
            container.RegisterSingleton<IValidator<Stack>, StackValidator>();
            container.RegisterSingleton<IValidator<Models.Token>, TokenValidator>();
            container.RegisterSingleton<IValidator<UserDescription>, UserDescriptionValidator>();
            container.RegisterSingleton<IValidator<User>, UserValidator>();
            container.RegisterSingleton<IValidator<WebHook>, WebHookValidator>();

            container.RegisterSingleton<IEmailGenerator>(() => new RazorEmailGenerator(@"Mail\Templates"));
            container.RegisterSingleton<IMailer, Mailer>();
            if (Settings.Current.WebsiteMode != WebsiteMode.Dev)
                container.RegisterSingleton<IMailSender, SmtpMailSender>();
            else
                container.RegisterSingleton<IMailSender>(() => new InMemoryMailSender());

            container.Register<ILockProvider, CacheLockProvider>();
            container.Register<StripeEventHandler>();
            container.RegisterSingleton<BillingManager>();
            container.RegisterSingleton<DataHelper>();
            container.RegisterSingleton<EventStats>();
            container.RegisterSingleton<EventPipeline>();
            container.RegisterSingleton<EventPluginManager>();
            container.RegisterSingleton<FormattingPluginManager>();

            container.RegisterSingleton<ICoreLastReferenceIdManager, NullCoreLastReferenceIdManager>();
        }
    }
}