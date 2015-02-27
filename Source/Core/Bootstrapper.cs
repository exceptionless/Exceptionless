#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

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
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Validation;
using Exceptionless.Models;
using Exceptionless.Models.Admin;
using Exceptionless.Models.Data;
using Exceptionless.Serializer;
using FluentValidation;
using Foundatio.AppStats;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Redis.Cache;
using Foundatio.Redis.Messaging;
using Foundatio.Redis.Queues;
using Foundatio.Storage;
using MongoDB.Driver;
using Nest;
using Newtonsoft.Json;
using RazorSharpEmail;
using SimpleInjector;
using SimpleInjector.Packaging;
using StackExchange.Redis;
using Token = Exceptionless.Models.Admin.Token;

namespace Exceptionless.Core {
    public class Bootstrapper : IPackage {
        public void RegisterServices(Container container) {
            container.RegisterSingle<IDependencyResolver>(() => new SimpleInjectorCoreDependencyResolver(container));

            //ExceptionlessClient.Default.Configuration.UseLogger(new Exceptionless.NLog.NLogExceptionlessLog());
            ExceptionlessClient.Default.Configuration.UseInMemoryStorage();
            ExceptionlessClient.Default.Configuration.UseReferenceIds();
            ExceptionlessClient.Default.Configuration.SetVersion(ThisAssembly.AssemblyInformationalVersion);
            container.RegisterSingle<ExceptionlessClient>(() => ExceptionlessClient.Default);

            if (Settings.Current.EnableAppStats)
                container.RegisterSingle<IMetricsClient>(() => new StatsDMetricsClient(Settings.Current.AppStatsServerName, Settings.Current.AppStatsServerPort));
            else {
                var metricsClient = new InMemoryMetricsClient();
                metricsClient.StartDisplayingStats();
                container.RegisterSingle<IMetricsClient>(metricsClient);
            }

            container.RegisterSingle<IDependencyResolver>(() => new SimpleInjectorCoreDependencyResolver(container));
            container.RegisterSingle<JsonSerializerSettings>(() => {
                var settings = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore, ContractResolver = new ExceptionlessContractResolver() };
                settings.AddModelConverters();

                return settings;
            });

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

            if (Settings.Current.EnableRedis) {
                var muxer = ConnectionMultiplexer.Connect(Settings.Current.RedisConnectionString);
                container.RegisterSingle(muxer);

                container.RegisterSingle<ICacheClient, RedisHybridCacheClient>();

                container.RegisterSingle<IQueue<EventPost>>(() => new RedisQueue<EventPost>(muxer, statName: MetricNames.PostsQueueSize, metrics: container.GetInstance<IMetricsClient>()));
                container.RegisterSingle<IQueue<EventUserDescription>>(() => new RedisQueue<EventUserDescription>(muxer, statName: MetricNames.EventsUserDescriptionQueueSize, metrics: container.GetInstance<IMetricsClient>()));
                container.RegisterSingle<IQueue<EventNotification>>(() => new RedisQueue<EventNotification>(muxer, statName: MetricNames.EventNotificationQueueSize, metrics: container.GetInstance<IMetricsClient>()));
                container.RegisterSingle<IQueue<WebHookNotification>>(() => new RedisQueue<WebHookNotification>(muxer, statName: MetricNames.WebHookQueueSize, metrics: container.GetInstance<IMetricsClient>()));
                container.RegisterSingle<IQueue<MailMessage>>(() => new RedisQueue<MailMessage>(muxer, statName: MetricNames.EmailsQueueSize, metrics: container.GetInstance<IMetricsClient>()));

                container.RegisterSingle<IMessageBus>(() => new RedisMessageBus(muxer.GetSubscriber()));
            } else {
                container.RegisterSingle<ICacheClient, InMemoryCacheClient>();

                container.RegisterSingle<IQueue<EventPost>>(() => new InMemoryQueue<EventPost>(statName: MetricNames.PostsQueueSize, metrics: container.GetInstance<IMetricsClient>()));
                container.RegisterSingle<IQueue<EventUserDescription>>(() => new InMemoryQueue<EventUserDescription>(statName: MetricNames.EventsUserDescriptionQueueSize, metrics: container.GetInstance<IMetricsClient>()));
                container.RegisterSingle<IQueue<EventNotification>>(() => new InMemoryQueue<EventNotification>(statName: MetricNames.EventNotificationQueueSize, metrics: container.GetInstance<IMetricsClient>()));
                container.RegisterSingle<IQueue<WebHookNotification>>(() => new InMemoryQueue<WebHookNotification>(statName: MetricNames.WebHookQueueSize, metrics: container.GetInstance<IMetricsClient>()));
                container.RegisterSingle<IQueue<MailMessage>>(() => new InMemoryQueue<MailMessage>(statName: MetricNames.EmailsQueueSize, metrics: container.GetInstance<IMetricsClient>()));

                container.RegisterSingle<IMessageBus, InMemoryMessageBus>();
            }
            container.RegisterSingle<IMessagePublisher>(container.GetInstance<IMessageBus>);
            container.RegisterSingle<IMessageSubscriber>(container.GetInstance<IMessageBus>);

            if (Settings.Current.EnableAzureStorage)
                container.RegisterSingle<IFileStorage>(new AzureFileStorage(Settings.Current.AzureStorageConnectionString));
            else if (!String.IsNullOrEmpty(Settings.Current.StorageFolder))
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
            container.RegisterSingle<IValidator<Token>, TokenValidator>();
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
        }
    }
}