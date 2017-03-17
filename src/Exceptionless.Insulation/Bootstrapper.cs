using System;
using System.Threading;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Insulation.Geo;
using Exceptionless.Insulation.Redis;
using Exceptionless.NLog;
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
using LogLevel = Exceptionless.Logging.LogLevel;

namespace Exceptionless.Insulation {
    public class Bootstrapper {
        public static void RegisterServices(Container container, bool runMaintenanceTasks, ILoggerFactory loggerFactory, CancellationToken shutdownCancellationToken) {
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
                    container.RegisterSingleton<ICacheClient>(() => new ScopedCacheClient(new RedisCacheClient(container.GetInstance<ConnectionMultiplexer>(), loggerFactory: loggerFactory), Settings.Current.AppScope));
                else
                    container.RegisterSingleton<ICacheClient, RedisCacheClient>();

                if (Settings.Current.EnableSignalR)
                    container.RegisterSingleton<IConnectionMapping, RedisConnectionMapping>();

                container.RegisterSingleton<IQueue<EventPost>>(() => new RedisQueue<EventPost>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<EventPost>(), behaviors: container.GetAllInstances<IQueueBehavior<EventPost>>(), runMaintenanceTasks: runMaintenanceTasks, loggerFactory: loggerFactory));
                container.RegisterSingleton<IQueue<EventUserDescription>>(() => new RedisQueue<EventUserDescription>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<EventUserDescription>(), behaviors: container.GetAllInstances<IQueueBehavior<EventUserDescription>>(), runMaintenanceTasks: runMaintenanceTasks, loggerFactory: loggerFactory));
                container.RegisterSingleton<IQueue<EventNotificationWorkItem>>(() => new RedisQueue<EventNotificationWorkItem>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<EventNotificationWorkItem>(), behaviors: container.GetAllInstances<IQueueBehavior<EventNotificationWorkItem>>(), runMaintenanceTasks: runMaintenanceTasks, loggerFactory: loggerFactory));
                container.RegisterSingleton<IQueue<WebHookNotification>>(() => new RedisQueue<WebHookNotification>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<WebHookNotification>(), behaviors: container.GetAllInstances<IQueueBehavior<WebHookNotification>>(), runMaintenanceTasks: runMaintenanceTasks, loggerFactory: loggerFactory));
                container.RegisterSingleton<IQueue<MailMessage>>(() => new RedisQueue<MailMessage>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<MailMessage>(), behaviors: container.GetAllInstances<IQueueBehavior<MailMessage>>(), runMaintenanceTasks: runMaintenanceTasks, loggerFactory: loggerFactory));
                container.RegisterSingleton<IQueue<WorkItemData>>(() => new RedisQueue<WorkItemData>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<WorkItemData>(), workItemTimeout: TimeSpan.FromHours(1), behaviors: container.GetAllInstances<IQueueBehavior<WorkItemData>>(), runMaintenanceTasks: runMaintenanceTasks, loggerFactory: loggerFactory));

                container.RegisterSingleton<IMessageBus>(() => new RedisMessageBus(container.GetInstance<ConnectionMultiplexer>().GetSubscriber(), $"{Settings.Current.AppScopePrefix}messages", container.GetInstance<ISerializer>(), loggerFactory));
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

            client.Configuration.UseLogger(new NLogExceptionlessLog(LogLevel.Warn));
            client.Configuration.SetDefaultMinLogLevel(LogLevel.Warn);
            client.Configuration.UpdateSettingsWhenIdleInterval = TimeSpan.FromSeconds(15);
            client.Configuration.SetVersion(Settings.Current.Version);
            if (String.IsNullOrEmpty(Settings.Current.InternalProjectId))
                client.Configuration.Enabled = false;

            client.Startup();
            container.AddStartupAction(() => client.RegisterWebApi(container.GetInstance<HttpConfiguration>()));
            client.Configuration.UseInMemoryStorage();
            client.Configuration.UseReferenceIds();
        }

        private static string GetQueueName<T>() {
            return String.Concat(Settings.Current.AppScopePrefix, typeof(T).Name);
        }
    }
}