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
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using MongoDB.Driver;
using RazorSharpEmail;
using ServiceStack.CacheAccess;
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
                container.RegisterSingle<IAppStatsClient, NullAppStatsClient>();

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

            container.Register<ErrorStatsHelper>();

            container.RegisterSingle<IErrorStackRepository, ErrorStackRepository>();
            container.RegisterSingle<IErrorRepository, ErrorRepository>();
            container.RegisterSingle<IOrganizationRepository, OrganizationRepository>();
            container.RegisterSingle<IJobLockInfoRepository, JobLockRepository>();
            container.RegisterSingle<IJobHistoryRepository, JobHistoryRepository>();
            container.RegisterSingle<IProjectRepository, ProjectRepository>();
            container.RegisterSingle<IUserRepository, UserRepository>();
            container.RegisterSingle<IProjectHookRepository, ProjectHookRepository>();
            container.RegisterSingle<MongoCollection<User>>(() => container.GetInstance<UserRepository>().Collection);

            container.RegisterSingle<IEmailGenerator>(() => new RazorEmailGenerator(@"Mail\Templates"));
            container.RegisterSingle<IMailer, Mailer>();

            container.RegisterSingle<IMessageService, ExceptionlessMqServer>();
            container.Register<IMessageFactory>(() => container.GetInstance<IMessageService>().MessageFactory);

            container.Register<IDependencyResolver, SimpleInjectorCoreDependencyResolver>();

            container.Register<MongoJobHistoryProvider>();
            container.Register<MongoJobLockProvider>();
            container.Register<MongoMachineJobLockProvider>();
            container.Register<StartMqJob>();
            container.Register<ErrorSignatureFactory>();
            container.Register<StripeEventHandler>();
            container.RegisterSingle<BillingManager>();
            container.RegisterSingle<ResetDataHelper>();
        }
    }
}