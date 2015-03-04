using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Foundatio.Queues;
using Foundatio.Redis.Queues;
using Foundatio.ServiceProvider;
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

            var manager = container.GetInstance<IStartupManager>();
            manager.Startup();

            return container;
        }
    }
}
