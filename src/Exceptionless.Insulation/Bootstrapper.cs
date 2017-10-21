using System;
using System.Linq;
using Exceptionless.Core;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Insulation.Geo;
using Exceptionless.Insulation.Mail;
using Exceptionless.Insulation.Redis;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Serializer;
using Foundatio.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Exceptionless.Insulation {
    public class Bootstrapper {
        public static void RegisterServices(IServiceCollection container, bool runMaintenanceTasks) {
            if (!String.IsNullOrEmpty(Settings.Current.ExceptionlessApiKey) && !String.IsNullOrEmpty(Settings.Current.ExceptionlessServerUrl)) {
                var client = ExceptionlessClient.Default;
                client.Configuration.ServerUrl = Settings.Current.ExceptionlessServerUrl;
                client.Configuration.ApiKey = Settings.Current.ExceptionlessApiKey;

                //client.Configuration.UseLogger(new NLogExceptionlessLog(LogLevel.Warn));
                client.Configuration.SetDefaultMinLogLevel(Logging.LogLevel.Warn);
                client.Configuration.UpdateSettingsWhenIdleInterval = TimeSpan.FromSeconds(15);
                client.Configuration.SetVersion(Settings.Current.Version);
                if (String.IsNullOrEmpty(Settings.Current.InternalProjectId))
                    client.Configuration.Enabled = false;

                client.Configuration.UseInMemoryStorage();
                client.Configuration.UseReferenceIds();

                container.AddSingleton<ICoreLastReferenceIdManager, ExceptionlessClientCoreLastReferenceIdManager>();
                container.AddSingleton<ExceptionlessClient>(client);
            }

            if (!String.IsNullOrEmpty(Settings.Current.GoogleGeocodingApiKey))
                container.AddSingleton<IGeocodeService>(s => new GoogleGeocodeService(Settings.Current.GoogleGeocodingApiKey));

            if (Settings.Current.EnableMetricsReporting)
                container.AddSingleton<IMetricsClient>(s => new StatsDMetricsClient(new StatsDMetricsClientOptions { ServerName = Settings.Current.MetricsServerName, Port = Settings.Current.MetricsServerPort, Prefix = "ex", LoggerFactory = s.GetRequiredService<ILoggerFactory>() }));

            if (Settings.Current.WebsiteMode != WebsiteMode.Dev)
                container.AddSingleton<IMailSender, MailKitMailSender>();

            if (Settings.Current.EnableRedis) {
                container.AddSingleton<ConnectionMultiplexer>(s => {
                    var multiplexer = ConnectionMultiplexer.Connect(Settings.Current.RedisConnectionString);
                    multiplexer.PreserveAsyncOrder = false;
                    return multiplexer;
                });

                if (Settings.Current.HasAppScope)
                    container.AddSingleton<ICacheClient>(s => new ScopedCacheClient(CreateRedisCacheClient(s), Settings.Current.AppScope));
                else
                    container.AddSingleton<ICacheClient>(CreateRedisCacheClient);

                if (!Settings.Current.DisableWebSockets)
                    container.AddSingleton<IConnectionMapping, RedisConnectionMapping>();

                container.AddSingleton<IMessageBus>(s => new RedisMessageBus(new RedisMessageBusOptions {
                    Subscriber = s.GetRequiredService<ConnectionMultiplexer>().GetSubscriber(),
                    Topic = $"{Settings.Current.AppScopePrefix}messages",
                    Serializer = s.GetRequiredService<ISerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            }

            if (Settings.Current.EnableAzureStorage) {
                container.AddSingleton(s => CreateAzureStorageQueue<EventPost>(s, retries: 1));
                container.AddSingleton(s => CreateAzureStorageQueue<EventUserDescription>(s));
                container.AddSingleton(s => CreateAzureStorageQueue<EventNotificationWorkItem>(s));
                container.AddSingleton(s => CreateAzureStorageQueue<WebHookNotification>(s));
                container.AddSingleton(s => CreateAzureStorageQueue<MailMessage>(s));
                container.AddSingleton(s => CreateAzureStorageQueue<WorkItemData>(s, workItemTimeout: TimeSpan.FromHours(1)));
            } else if (Settings.Current.EnableRedis) {
                container.AddSingleton(s => CreateRedisQueue<EventPost>(s, runMaintenanceTasks, retries: 1));
                container.AddSingleton(s => CreateRedisQueue<EventUserDescription>(s, runMaintenanceTasks));
                container.AddSingleton(s => CreateRedisQueue<EventNotificationWorkItem>(s, runMaintenanceTasks));
                container.AddSingleton(s => CreateRedisQueue<WebHookNotification>(s, runMaintenanceTasks));
                container.AddSingleton(s => CreateRedisQueue<MailMessage>(s, runMaintenanceTasks));
                container.AddSingleton(s => CreateRedisQueue<WorkItemData>(s, runMaintenanceTasks, workItemTimeout: TimeSpan.FromHours(1)));
            }

            if (Settings.Current.EnableAzureStorage)
                container.AddSingleton<IFileStorage>(s => new AzureFileStorage(Settings.Current.AzureStorageConnectionString, $"{Settings.Current.AppScopePrefix}ex-events"));
        }

        private static IQueue<T> CreateAzureStorageQueue<T>(IServiceProvider container, int retries = 2, TimeSpan? workItemTimeout = null) where T : class {
            return new AzureStorageQueue<T>(new AzureStorageQueueOptions<T> {
                ConnectionString = Settings.Current.AzureStorageConnectionString,
                Name = GetQueueName<T>().ToLowerInvariant(),
                Retries = retries,
                Behaviors = container.GetServices<IQueueBehavior<T>>().ToList(),
                WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
                Serializer = container.GetRequiredService<ISerializer>(),
                LoggerFactory = container.GetRequiredService<ILoggerFactory>()
            });
        }

        private static IQueue<T> CreateRedisQueue<T>(IServiceProvider container, bool runMaintenanceTasks, int retries = 2, TimeSpan? workItemTimeout = null) where T : class {
            return new RedisQueue<T>(new RedisQueueOptions<T> {
                ConnectionMultiplexer = container.GetRequiredService<ConnectionMultiplexer>(),
                Name = GetQueueName<T>(),
                Retries = retries,
                Behaviors = container.GetServices<IQueueBehavior<T>>().ToList(),
                WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
                RunMaintenanceTasks = runMaintenanceTasks,
                Serializer = container.GetRequiredService<ISerializer>(),
                LoggerFactory = container.GetRequiredService<ILoggerFactory>()
            });
        }

        private static string GetQueueName<T>() {
            return String.Concat(Settings.Current.AppScopePrefix, typeof(T).Name);
        }

        private static RedisCacheClient CreateRedisCacheClient(IServiceProvider container) {
            return new RedisCacheClient(new RedisCacheClientOptions {
                ConnectionMultiplexer = container.GetRequiredService<ConnectionMultiplexer>(),
                Serializer = container.GetRequiredService<ISerializer>(),
                LoggerFactory = container.GetRequiredService<ILoggerFactory>()
            });
        }
    }
}