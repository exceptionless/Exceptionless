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
using System.Diagnostics;
using CodeSmith.Core.Dependency;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Plugins.EventPipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using MongoDB.Driver;
using RazorSharpEmail;
using ServiceStack.CacheAccess;
using ServiceStack.Common;
using ServiceStack.Messaging;
using ServiceStack.Redis;
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

            container.RegisterSingle<IRedisClientsManager>(() => new PooledRedisClientManager(Settings.Current.RedisConnectionInfo.ToString()));
            container.Register<ICacheClient>(() => container.GetInstance<IRedisClientsManager>().GetCacheClient());

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

            string serviceBusConnectionString = "";
            container.RegisterSingle<IQueue<EventPost>>(() => new InMemoryQueue<EventPost>());

            container.Register<EventStatsHelper>();

            container.RegisterSingleImplementation<StackRepository>(typeof(IStackRepository), typeof(StackRepository));
            container.RegisterSingleImplementation<EventRepository>(typeof(IEventRepository), typeof(EventRepository));
            container.RegisterSingleImplementation<OrganizationRepository>(typeof(IOrganizationRepository), typeof(OrganizationRepository));
            container.RegisterSingleImplementation<JobLockRepository>(typeof(IJobLockInfoRepository), typeof(JobLockRepository));
            container.RegisterSingleImplementation<JobHistoryRepository>(typeof(IJobHistoryRepository), typeof(JobHistoryRepository));
            container.RegisterSingleImplementation<ProjectRepository>(typeof(IProjectRepository), typeof(ProjectRepository));
            container.RegisterSingleImplementation<UserRepository>(typeof(IUserRepository), typeof(UserRepository));
            container.RegisterSingleImplementation<ProjectHookRepository>(typeof(IProjectHookRepository), typeof(ProjectHookRepository));
            container.RegisterSingle<DayProjectStatsRepository>();
            container.RegisterSingle<MonthProjectStatsRepository>();
            container.RegisterSingle<MonthStackStatsRepository>();
            container.RegisterSingle<DayStackStatsRepository>();

            container.RegisterSingle<MongoCollection<User>>(() => container.GetInstance<UserRepository>().Collection);

            container.RegisterSingle<IEmailGenerator>(() => new RazorEmailGenerator(@"Mail\Templates"));
            container.RegisterSingle<IMailer, Mailer>();

            container.RegisterSingle<IMessageService, ExceptionlessMqServer>();
            container.Register<IMessageFactory>(() => container.GetInstance<IMessageService>().MessageFactory);

            container.Register<MongoJobHistoryProvider>();
            container.Register<MongoJobLockProvider>();
            container.Register<MongoMachineJobLockProvider>();
            container.Register<StartMqJob>();
            container.Register<StripeEventHandler>();
            container.RegisterSingle<BillingManager>();
            container.RegisterSingle<DataHelper>();
            container.RegisterSingle<EventPluginManager>();
            container.RegisterSingle<FormattingPluginManager>();
            
            var project1 = container.GetInstance(typeof(IProjectRepository));
            var project2 = container.GetInstance(typeof(ProjectRepository));
            Debug.Assert(ReferenceEquals(project1, project2));
            var resolver1 = container.GetInstance<IDependencyResolver>();
            var resolver2 = container.GetInstance<IDependencyResolver>();
            Debug.Assert(ReferenceEquals(resolver1, resolver2));
            var project4 = resolver1.GetService(typeof(ProjectRepository));
            Debug.Assert(ReferenceEquals(project1, project4));
        }
    }
}