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

                container.RegisterSingleton<IMessageBus>(() => new RedisMessageBus(new RedisMessageBusOptions {
                    Subscriber = container.GetInstance<ConnectionMultiplexer>().GetSubscriber(),
                    Topic = $"{Settings.Current.AppScopePrefix}messages",
                    Serializer = container.GetInstance<ISerializer>(),
                    LoggerFactory = loggerFactory
                }));
            } else {
                logger.Warn("Redis is NOT enabled.");
            }

            if (Settings.Current.EnableAzureStorage) {
                container.RegisterSingleton(() => CreateAzureStorageQueue<EventPost>(container, loggerFactory: loggerFactory));
                container.RegisterSingleton(() => CreateAzureStorageQueue<EventUserDescription>(container, loggerFactory: loggerFactory));
                container.RegisterSingleton(() => CreateAzureStorageQueue<EventNotificationWorkItem>(container, loggerFactory: loggerFactory));
                container.RegisterSingleton(() => CreateAzureStorageQueue<WebHookNotification>(container, loggerFactory: loggerFactory));
                container.RegisterSingleton(() => CreateAzureStorageQueue<MailMessage>(container, loggerFactory: loggerFactory));
                container.RegisterSingleton(() => CreateAzureStorageQueue<WorkItemData>(container, TimeSpan.FromHours(1), loggerFactory));
            } else if (Settings.Current.EnableRedis) {
                container.RegisterSingleton(() => CreateRedisQueue<EventPost>(container, runMaintenanceTasks, loggerFactory: loggerFactory));
                container.RegisterSingleton(() => CreateRedisQueue<EventUserDescription>(container, runMaintenanceTasks, loggerFactory: loggerFactory));
                container.RegisterSingleton(() => CreateRedisQueue<EventNotificationWorkItem>(container, runMaintenanceTasks, loggerFactory: loggerFactory));
                container.RegisterSingleton(() => CreateRedisQueue<WebHookNotification>(container, runMaintenanceTasks, loggerFactory: loggerFactory));
                container.RegisterSingleton(() => CreateRedisQueue<MailMessage>(container, runMaintenanceTasks, loggerFactory: loggerFactory));
                container.RegisterSingleton(() => CreateRedisQueue<WorkItemData>(container, runMaintenanceTasks, TimeSpan.FromHours(1), loggerFactory));
            }

            if (Settings.Current.EnableAzureStorage)
                container.RegisterSingleton<IFileStorage>(() => new AzureFileStorage(Settings.Current.AzureStorageConnectionString, $"{Settings.Current.AppScopePrefix}ex-events"));
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

        private static IQueue<T> CreateAzureStorageQueue<T>(Container container, TimeSpan? workItemTimeout = null, ILoggerFactory loggerFactory = null) where T : class {
            return new AzureStorageQueue<T>(new AzureStorageQueueOptions<T> {
                ConnectionString = Settings.Current.AzureStorageConnectionString,
                Name = GetQueueName<T>().ToLowerInvariant(),
                Behaviors = container.GetAllInstances<IQueueBehavior<T>>(),
                WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
                Serializer = container.GetInstance<ISerializer>(),
                LoggerFactory = loggerFactory
            });
        }

        private static IQueue<T> CreateRedisQueue<T>(Container container, bool runMaintenanceTasks, TimeSpan? workItemTimeout = null, ILoggerFactory loggerFactory = null) where T : class {
            return new RedisQueue<T>(new RedisQueueOptions<T> {
                ConnectionMultiplexer = container.GetInstance<ConnectionMultiplexer>(),
                Name = GetQueueName<T>(),
                Behaviors = container.GetAllInstances<IQueueBehavior<T>>(),
                WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
                RunMaintenanceTasks = runMaintenanceTasks,
                Serializer = container.GetInstance<ISerializer>(),
                LoggerFactory = loggerFactory
            });
        }

        private static string GetQueueName<T>() {
            return String.Concat(Settings.Current.AppScopePrefix, typeof(T).Name);
        }
    }
}