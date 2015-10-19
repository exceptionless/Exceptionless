using System;
using System.Collections.Generic;
using System.Linq;
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
            
            container.RegisterSingleton<IMetricsClient, InMemoryMetricsClient>();
            container.RegisterSingleton<IElasticClient>(() => container.GetInstance<ElasticSearchConfiguration>().GetClient(Settings.Current.ElasticSearchConnectionString.Split(',').Select(url => new Uri(url))));
            container.RegisterSingleton<EventIndex, EventIndex>();
            container.RegisterSingleton<OrganizationIndex, OrganizationIndex>();
            container.RegisterSingleton<StackIndex, StackIndex>();

            container.RegisterSingleton<ICacheClient, InMemoryCacheClient>();
            
            container.RegisterSingleton<IEnumerable<IQueueBehavior<EventPost>>>(() => new[] { new MetricsQueueBehavior<EventPost>(container.GetInstance<IMetricsClient>()) });
            container.RegisterSingleton<IEnumerable<IQueueBehavior<EventUserDescription>>>(() => new[] { new MetricsQueueBehavior<EventUserDescription>(container.GetInstance<IMetricsClient>()) });
            container.RegisterSingleton<IEnumerable<IQueueBehavior<EventNotificationWorkItem>>>(() => new[] { new MetricsQueueBehavior<EventNotificationWorkItem>(container.GetInstance<IMetricsClient>()) });
            container.RegisterSingleton<IEnumerable<IQueueBehavior<WebHookNotification>>>(() => new[] { new MetricsQueueBehavior<WebHookNotification>(container.GetInstance<IMetricsClient>()) });
            container.RegisterSingleton<IEnumerable<IQueueBehavior<MailMessage>>>(() => new[] { new MetricsQueueBehavior<MailMessage>(container.GetInstance<IMetricsClient>()) });
            container.RegisterSingleton<IEnumerable<IQueueBehavior<WorkItemData>>>(() => new[] { new MetricsQueueBehavior<WorkItemData>(container.GetInstance<IMetricsClient>()) });
            
            container.RegisterSingleton<IQueue<EventPost>>(() => new InMemoryQueue<EventPost>(behaviors: container.GetAllInstances<IQueueBehavior<EventPost>>()));
            container.RegisterSingleton<IQueue<EventUserDescription>>(() => new InMemoryQueue<EventUserDescription>(behaviors: container.GetAllInstances<IQueueBehavior<EventUserDescription>>()));
            container.RegisterSingleton<IQueue<EventNotificationWorkItem>>(() => new InMemoryQueue<EventNotificationWorkItem>(behaviors: container.GetAllInstances<IQueueBehavior<EventNotificationWorkItem>>()));
            container.RegisterSingleton<IQueue<WebHookNotification>>(() => new InMemoryQueue<WebHookNotification>(behaviors: container.GetAllInstances<IQueueBehavior<WebHookNotification>>()));
            container.RegisterSingleton<IQueue<MailMessage>>(() => new InMemoryQueue<MailMessage>(behaviors: container.GetAllInstances<IQueueBehavior<MailMessage>>()));
            container.RegisterSingleton<IQueue<StatusMessage>>(() => new InMemoryQueue<StatusMessage>());

            var workItemHandlers = new WorkItemHandlers();
            workItemHandlers.Register<ReindexWorkItem, ReindexWorkItemHandler>();
            workItemHandlers.Register<RemoveOrganizationWorkItem, RemoveOrganizationWorkItemHandler>();
            workItemHandlers.Register<RemoveProjectWorkItem, RemoveProjectWorkItemHandler>();
            workItemHandlers.Register<SetProjectIsConfiguredWorkItem, SetProjectIsConfiguredWorkItemHandler>();
            workItemHandlers.Register<StackWorkItem, StackWorkItemHandler>();
            workItemHandlers.Register<ThrottleBotsWorkItem, ThrottleBotsWorkItemHandler>();
            container.RegisterSingleton<WorkItemHandlers>(workItemHandlers);
            container.RegisterSingleton<IQueue<WorkItemData>>(() => new InMemoryQueue<WorkItemData>(behaviors: container.GetAllInstances<IQueueBehavior<WorkItemData>>(), workItemTimeout: TimeSpan.FromHours(1)));
            
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

            container.RegisterSingleton<ILockProvider, CacheLockProvider>();
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