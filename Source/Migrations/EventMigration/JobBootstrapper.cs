using System;
using System.Reflection;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Foundatio.Queues;
using Foundatio.Redis.Queues;
using Foundatio.ServiceProvider;
using NLog.Fluent;
using SimpleInjector;
using StackExchange.Redis;

namespace Exceptionless.EventMigration {
    public class JobBootstrapper : BootstrappedServiceProviderBase {
        public override IServiceProvider Bootstrap() {
            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            container.Options.PropertySelectionBehavior = new InjectAttributePropertySelectionBehavior();

            container.RegisterPackage<Bootstrapper>();

            if (Settings.Current.EnableRedis)
                container.RegisterSingle<IQueue<EventMigrationBatch>>(() => new RedisQueue<EventMigrationBatch>(container.GetInstance<ConnectionMultiplexer>(), retries: 5, workItemTimeout: TimeSpan.FromHours(2)));
            else
                container.RegisterSingle<IQueue<EventMigrationBatch>>(() => new InMemoryQueue<EventMigrationBatch>(retries: 5, workItemTimeout: TimeSpan.FromHours(2)));

            Assembly insulationAssembly = null;
            try {
                insulationAssembly = Assembly.Load("Exceptionless.Insulation");
            } catch (Exception ex) {
                Log.Error().Message("Unable to load the insulation asssembly.").Exception(ex).Write();
            }

            if (insulationAssembly != null)
                container.RegisterPackages(new[] { insulationAssembly });

            return container;
        }
    }
}
