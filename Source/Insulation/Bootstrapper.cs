using System;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Insulation.Geo;
using Exceptionless.Plugins.Default;
using Exceptionless.Insulation.Redis;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Logging.NLog;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Serializer;
using Foundatio.Storage;
using SimpleInjector;
using StackExchange.Redis;

namespace Exceptionless.Insulation {
    public class Bootstrapper {
        public static void RegisterServices(Container container, ILoggerFactory loggerFactory) {
            loggerFactory.AddNLog();
            var logger = loggerFactory.CreateLogger<Bootstrapper>();

            if (!String.IsNullOrEmpty(Settings.Current.GoogleGeocodingApiKey))
                container.RegisterSingleton<IGeocodeService>(() => new GoogleGeocodeService(Settings.Current.GoogleGeocodingApiKey));
            
            if (Settings.Current.EnableMetricsReporting)
                container.RegisterSingleton<IMetricsClient>(() => new StatsDMetricsClient(Settings.Current.MetricsServerName, Settings.Current.MetricsServerPort, "ex"));
            else
                logger.Warn("StatsD Metrics is NOT enabled.");

            if (Settings.Current.EnableRedis) {
                container.RegisterSingleton<ConnectionMultiplexer>(() => {
                    var multiplexer = ConnectionMultiplexer.Connect(Settings.Current.RedisConnectionString);
                    multiplexer.PreserveAsyncOrder = false;
                    return multiplexer;
                });

                if (Settings.Current.HasAppScope)
                    container.RegisterSingleton<ICacheClient>(() => new ScopedCacheClient(new RedisHybridCacheClient(container.GetInstance<ConnectionMultiplexer>()), Settings.Current.AppScope));
                else
                    container.RegisterSingleton<ICacheClient, RedisHybridCacheClient>();

                if (Settings.Current.EnableSignalR)
                    container.RegisterSingleton<IConnectionMapping, RedisConnectionMapping>();

                container.RegisterSingleton<IQueue<EventPost>>(() => new RedisQueue<EventPost>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<EventPost>(), behaviors: container.GetAllInstances<IQueueBehavior<EventPost>>()));
                container.RegisterSingleton<IQueue<EventUserDescription>>(() => new RedisQueue<EventUserDescription>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<EventUserDescription>(), behaviors: container.GetAllInstances<IQueueBehavior<EventUserDescription>>()));
                container.RegisterSingleton<IQueue<EventNotificationWorkItem>>(() => new RedisQueue<EventNotificationWorkItem>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<EventNotificationWorkItem>(), behaviors: container.GetAllInstances<IQueueBehavior<EventNotificationWorkItem>>()));
                container.RegisterSingleton<IQueue<WebHookNotification>>(() => new RedisQueue<WebHookNotification>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<WebHookNotification>(), behaviors: container.GetAllInstances<IQueueBehavior<WebHookNotification>>()));
                container.RegisterSingleton<IQueue<MailMessage>>(() => new RedisQueue<MailMessage>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<MailMessage>(), behaviors: container.GetAllInstances<IQueueBehavior<MailMessage>>()));
                container.RegisterSingleton<IQueue<WorkItemData>>(() => new RedisQueue<WorkItemData>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<WorkItemData>(), workItemTimeout: TimeSpan.FromHours(1), behaviors: container.GetAllInstances<IQueueBehavior<WorkItemData>>()));

                container.RegisterSingleton<IMessageBus>(() => new RedisMessageBus(container.GetInstance<ConnectionMultiplexer>().GetSubscriber(), $"{Settings.Current.AppScopePrefix}messages", container.GetInstance<ISerializer>()));
            } else {
                logger.Warn("Redis is NOT enabled.");
            }

            if (Settings.Current.EnableAzureStorage)
                container.RegisterSingleton<IFileStorage>(new AzureFileStorage(Settings.Current.AzureStorageConnectionString, $"{Settings.Current.AppScopePrefix}ex-events"));
            else
                logger.Warn("Azure Storage is NOT enabled.");

            var client = ExceptionlessClient.Default;
            container.RegisterSingleton<ICoreLastReferenceIdManager, ExceptionlessClientCoreLastReferenceIdManager>();
            container.RegisterSingleton<ExceptionlessClient>(() => client);

            client.Configuration.RemovePlugin<UpdateConfigurationSettingsWhileIdlePlugin>();
            client.Configuration.AddPlugin(new UpdateConfigurationSettingsWhileIdlePlugin(client.Configuration, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)));

            client.Configuration.SetVersion(Settings.Current.Version);
            if (String.IsNullOrEmpty(Settings.Current.InternalProjectId))
                client.Configuration.Enabled = false;

            client.Register();
            container.AddBootstrapper<HttpConfiguration>(config => client.RegisterWebApi(config));
            client.Configuration.UseInMemoryStorage();
            client.Configuration.UseReferenceIds();
        }

        private static string GetQueueName<T>() {
            return String.Concat(Settings.Current.AppScopePrefix, typeof(T).Name);
        }
    }
}
