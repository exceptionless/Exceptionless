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
using System.Reflection;
using CodeSmith.Core.Dependency;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Extensions;
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
using Exceptionless.Core.Validation;
using Exceptionless.Models;
using Exceptionless.Models.Admin;
using Exceptionless.Models.Data;
using FluentValidation;
using MongoDB.Driver;
using Nest;
using Nest.Resolvers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RazorSharpEmail;
using SimpleInjector;
using SimpleInjector.Packaging;
using Token = Exceptionless.Models.Admin.Token;

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

            container.RegisterSingle<ElasticClient>(() => {
                var settings = new ConnectionSettings(new Uri("http://localhost:9200")).SetDefaultIndex("exceptionless_v1");
                settings.EnableTrace();
                settings.SetJsonSerializerSettingsModifier(s => { s.ContractResolver = new EmptyCollectionContractResolver(settings); });
                settings.MapDefaultTypeNames(m => m.Add(typeof(PersistentEvent), "events").Add(typeof(Stack), "stacks"));
                settings.SetDefaultPropertyNameInferrer(p => p.ToLowerUnderscoredWords());
                return new ElasticClient(settings);
            });

            container.RegisterSingle<IQueue<EventPost>>(() => new InMemoryQueue<EventPost>());
            container.RegisterSingle<IQueue<EventUserDescription>>(() => new InMemoryQueue<EventUserDescription>(workItemTimeoutMilliseconds: 2 * 60 * 1000));
            container.RegisterSingle<IQueue<EventNotification>>(() => new InMemoryQueue<EventNotification>());
            container.RegisterSingle<IQueue<WebHookNotification>>(() => new InMemoryQueue<WebHookNotification>());
            container.RegisterSingle<IQueue<MailMessage>>(() => new InMemoryQueue<MailMessage>());
            
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

            container.RegisterSingle<IValidator<Application>, ApplicationValidator>();
            container.RegisterSingle<IValidator<DayProjectStats>, DayProjectStatsValidator>();
            container.RegisterSingle<IValidator<DayStackStats>, DayStackStatsValidator>();
            container.RegisterSingle<IValidator<Event>, EventValidator>();
            container.RegisterSingle<IValidator<JobHistory>, JobHistoryValidator>();
            container.RegisterSingle<IValidator<JobLockInfo>, JobLockInfoValidator>();
            container.RegisterSingle<IValidator<MonthProjectStats>, MonthProjectStatsValidator>();
            container.RegisterSingle<IValidator<MonthStackStats>, MonthStackStatsValidator>();
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

    public class EmptyCollectionContractResolver : ElasticContractResolver {
        public EmptyCollectionContractResolver(IConnectionSettingsValues connectionSettings) : base(connectionSettings) {}

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            Predicate<object> shouldSerialize = property.ShouldSerialize;
            property.ShouldSerialize = obj => (shouldSerialize == null || shouldSerialize(obj)) && !property.IsValueEmptyCollection(obj);
            return property;
        }

        protected override JsonDictionaryContract CreateDictionaryContract(Type objectType) {
            if (objectType != typeof(DataDictionary) && objectType != typeof(SettingsDictionary))
                return base.CreateDictionaryContract(objectType);

            JsonDictionaryContract contract = base.CreateDictionaryContract(objectType);
            contract.PropertyNameResolver = propertyName => propertyName;
            return contract;
        }

        protected override string ResolvePropertyName(string propertyName) {
            return propertyName.ToLowerUnderscoredWords();
        }
    }
}