using System;
using System.Configuration;
using System.Linq;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Validation;
using Exceptionless.Core.Models.Admin;
using Exceptionless.Core.Models.Data;
using Exceptionless.Serializer;
using FluentValidation;
using Foundatio.Metrics;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Serializer;
using Foundatio.Storage;
using MongoDB.Driver;
using Nest;
using Newtonsoft.Json;
using RazorSharpEmail;
using SimpleInjector;
using SimpleInjector.Packaging;

namespace Exceptionless.Core {
    public class Bootstrapper : IPackage {
        public void RegisterServices(Container container) {
            container.RegisterSingle<IDependencyResolver>(() => new SimpleInjectorCoreDependencyResolver(container));

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings {
                DateParseHandling = DateParseHandling.DateTimeOffset
            };


            var settings = new JsonSerializerSettings {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                DateParseHandling = DateParseHandling.DateTimeOffset,
                ContractResolver = new ExceptionlessContractResolver()
            };

            settings.AddModelConverters();
            container.RegisterSingle<JsonSerializerSettings>(settings);
            container.RegisterSingle<ISerializer>(() => new JsonNetSerializer(settings));

            var metricsClient = new InMemoryMetricsClient();
            metricsClient.StartDisplayingStats();
            container.RegisterSingle<IMetricsClient>(metricsClient);

            container.RegisterSingle<MongoDatabase>(() => {
                if (String.IsNullOrEmpty(Settings.Current.MongoConnectionString))
                    throw new ConfigurationErrorsException("MongoConnectionString was not found in the Web.config.");

                MongoDefaults.MaxConnectionIdleTime = TimeSpan.FromMinutes(1);
                var url = new MongoUrl(Settings.Current.MongoConnectionString);
                string databaseName = url.DatabaseName;
                if (Settings.Current.AppendMachineNameToDatabase)
                    databaseName += String.Concat("-", Environment.MachineName.ToLower());

                MongoServer server = new MongoClient(url).GetServer();
                return server.GetDatabase(databaseName);
            });

            container.RegisterSingle<IElasticClient>(() => ElasticSearchConfiguration.GetElasticClient(Settings.Current.ElasticSearchConnectionString.Split(',').Select(url => new Uri(url))));

            container.RegisterSingle<ICacheClient, InMemoryCacheClient>();

            container.RegisterSingle<IQueue<EventPost>>(() => new InMemoryQueue<EventPost>(statName: MetricNames.PostsQueueSize, metrics: container.GetInstance<IMetricsClient>()));
            container.RegisterSingle<IQueue<EventUserDescription>>(() => new InMemoryQueue<EventUserDescription>(statName: MetricNames.EventsUserDescriptionQueueSize, metrics: container.GetInstance<IMetricsClient>()));
            container.RegisterSingle<IQueue<EventNotificationWorkItem>>(() => new InMemoryQueue<EventNotificationWorkItem>(statName: MetricNames.EventNotificationQueueSize, metrics: container.GetInstance<IMetricsClient>()));
            container.RegisterSingle<IQueue<WebHookNotification>>(() => new InMemoryQueue<WebHookNotification>(statName: MetricNames.WebHookQueueSize, metrics: container.GetInstance<IMetricsClient>()));
            container.RegisterSingle<IQueue<MailMessage>>(() => new InMemoryQueue<MailMessage>(statName: MetricNames.EmailsQueueSize, metrics: container.GetInstance<IMetricsClient>()));

            container.RegisterSingle<IMessageBus, InMemoryMessageBus>();

            container.RegisterSingle<IMessagePublisher>(container.GetInstance<IMessageBus>);
            container.RegisterSingle<IMessageSubscriber>(container.GetInstance<IMessageBus>);

            if (!String.IsNullOrEmpty(Settings.Current.StorageFolder))
                container.RegisterSingle<IFileStorage>(new FolderFileStorage(Settings.Current.StorageFolder));
            else
                container.RegisterSingle<IFileStorage>(new InMemoryFileStorage());

            container.RegisterSingle<IStackRepository, StackRepository>();
            container.RegisterSingle<IEventRepository, EventRepository>();
            container.RegisterSingle<IOrganizationRepository, OrganizationRepository>();
            container.RegisterSingle<IProjectRepository, ProjectRepository>();
            container.RegisterSingle<IUserRepository, UserRepository>();
            container.RegisterSingle<IWebHookRepository, WebHookRepository>();
            container.RegisterSingle<ITokenRepository, TokenRepository>();
            container.RegisterSingle<IApplicationRepository, ApplicationRepository>();

            container.RegisterSingle<IGeoIPResolver, MindMaxGeoIPResolver>();

            container.RegisterSingle<IValidator<Application>, ApplicationValidator>();
            container.RegisterSingle<IValidator<Organization>, OrganizationValidator>();
            container.RegisterSingle<IValidator<PersistentEvent>, PersistentEventValidator>();
            container.RegisterSingle<IValidator<Project>, ProjectValidator>();
            container.RegisterSingle<IValidator<Stack>, StackValidator>();
            container.RegisterSingle<IValidator<Models.Admin.Token>, TokenValidator>();
            container.RegisterSingle<IValidator<UserDescription>, UserDescriptionValidator>();
            container.RegisterSingle<IValidator<User>, UserValidator>();
            container.RegisterSingle<IValidator<WebHook>, WebHookValidator>();

            container.RegisterSingle<IEmailGenerator>(() => new RazorEmailGenerator(@"Mail\Templates"));
            container.RegisterSingle<IMailer, Mailer>();
            if (Settings.Current.WebsiteMode != WebsiteMode.Dev)
                container.RegisterSingle<IMailSender, SmtpMailSender>();
            else
                container.RegisterSingle<IMailSender>(() => new InMemoryMailSender());

            container.Register<ILockProvider, CacheLockProvider>();
            container.Register<StripeEventHandler>();
            container.RegisterSingle<BillingManager>();
            container.RegisterSingle<DataHelper>();
            container.RegisterSingle<EventStats>();
            container.RegisterSingle<EventPipeline>();
            container.RegisterSingle<EventPluginManager>();
            container.RegisterSingle<FormattingPluginManager>();

            container.RegisterSingle<ICoreLastReferenceIdManager, NullCoreLastReferenceIdManager>();
        }
    }
}