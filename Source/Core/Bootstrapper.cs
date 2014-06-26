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
using CodeSmith.Core.Dependency;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using MongoDB.Driver;
using RazorSharpEmail;
using SimpleInjector;
using SimpleInjector.Packaging;

namespace Exceptionless.Core {
    public class Bootstrapper : IPackage {
        public void RegisterServices(Container container) {
            container.RegisterSingle<IDependencyResolver>(() => new SimpleInjectorCoreDependencyResolver(container));

            if (Settings.Current.EnableAppStats)
                container.RegisterSingle<IAppStatsClient>(() => new AppStatsClient(Settings.Current.AppStatsServerName, Settings.Current.AppStatsServerPort));
            else
                container.RegisterSingle<IAppStatsClient, InMemoryAppStatsClient>();

            if (Settings.Current.RedisConnectionInfo == null)
                throw new ConfigurationErrorsException("RedisConnectionString was not found in the Web.config.");

            container.RegisterSingle<IDependencyResolver>(() => new SimpleInjectorCoreDependencyResolver(container));
            container.RegisterSingle<ICacheClient, InMemoryCacheClient>();

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

            container.RegisterSingle<IQueue<EventPost>>(() => new InMemoryQueue<EventPost>());
            container.RegisterSingle<IQueue<EventNotification>>(() => new InMemoryQueue<EventNotification>());
            container.RegisterSingle<IQueue<WebHookNotification>>(() => new InMemoryQueue<WebHookNotification>());

            container.Register<EventStatsHelper>();
            container.RegisterSingle<InMemoryMessageBus>();
            container.Register<IMessagePublisher>(container.GetInstance<InMemoryMessageBus>);
            container.Register<IMessageSubscriber>(container.GetInstance<InMemoryMessageBus>);

            container.RegisterSingle<IStackRepository, StackRepository>();
            container.RegisterSingle<IEventRepository, EventRepository>();
            container.RegisterSingle<IOrganizationRepository, OrganizationRepository>();
            container.RegisterSingle<IJobLockRepository, JobLockRepository>();
            container.RegisterSingle<IJobHistoryRepository, JobHistoryRepository>();
            container.RegisterSingle<IProjectRepository, ProjectRepository>();
            container.RegisterSingle<IUserRepository, UserRepository>();
            container.RegisterSingle<IWebHookRepository, WebHookRepository>();
            container.RegisterSingle<IDayProjectStatsRepository, DayProjectStatsRepository>();
            container.RegisterSingle<IMonthProjectStatsRepository, MonthProjectStatsRepository>();
            container.RegisterSingle<IMonthStackStatsRepository, MonthStackStatsRepository>();
            container.RegisterSingle<IDayStackStatsRepository, DayStackStatsRepository>();
            container.RegisterSingle<ITokenRepository, TokenRepository>();
            container.RegisterSingle<IApplicationRepository, ApplicationRepository>();

            container.RegisterSingle<IEmailGenerator>(() => new RazorEmailGenerator(@"Mail\Templates"));
            container.RegisterSingle<IMailer, Mailer>();

            container.Register<MongoJobHistoryProvider>();
            container.Register<MongoJobLockProvider>();
            container.Register<MongoMachineJobLockProvider>();
            container.Register<StripeEventHandler>();
            container.RegisterSingle<BillingManager>();
            container.RegisterSingle<DataHelper>();
            container.RegisterSingle<EventPluginManager>();
            container.RegisterSingle<FormattingPluginManager>();
        }
    }
}