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
        public static void RegisterServices(IServiceCollection container, bool runMaintenanceTasks, ILoggerFactory loggerFactory) {
            var logger = loggerFactory.CreateLogger<Bootstrapper>();

            if (!String.IsNullOrEmpty(Settings.Current.ExceptionlessApiKey) && !String.IsNullOrEmpty(Settings.Current.ExceptionlessServerUrl)) {
                var client = ExceptionlessClient.Default;
                client.Configuration.ServerUrl = Settings.Current.ExceptionlessServerUrl;
                client.Configuration.ApiKey = Settings.Current.ExceptionlessApiKey;

                //client.Configuration.UseLogger(new NLogExceptionlessLog(LogLevel.Warn));
                client.Configuration.SetDefaultMinLogLevel(Exceptionless.Logging.LogLevel.Warn);
                client.Configuration.UpdateSettingsWhenIdleInterval = TimeSpan.FromSeconds(15);
                client.Configuration.SetVersion(Settings.Current.Version);
                if (String.IsNullOrEmpty(Settings.Current.InternalProjectId))
                    client.Configuration.Enabled = false;

                client.Configuration.UseInMemoryStorage();
                client.Configuration.UseReferenceIds();
                loggerFactory.AddExceptionless(client);

                container.AddSingleton<ICoreLastReferenceIdManager, ExceptionlessClientCoreLastReferenceIdManager>();
                container.AddSingleton<ExceptionlessClient>(client);
            }

            if (!String.IsNullOrEmpty(Settings.Current.GoogleGeocodingApiKey))
                container.AddSingleton<IGeocodeService>(s => new GoogleGeocodeService(Settings.Current.GoogleGeocodingApiKey));

            if (Settings.Current.EnableMetricsReporting)
                container.AddSingleton<IMetricsClient>(s => new StatsDMetricsClient(new StatsDMetricsClientOptions { ServerName = Settings.Current.MetricsServerName, Port = Settings.Current.MetricsServerPort, Prefix = "ex", LoggerFactory = loggerFactory }));
            else
                logger.LogWarning("StatsD Metrics is NOT enabled.");

            if (Settings.Current.WebsiteMode != WebsiteMode.Dev)
                container.AddSingleton<IMailSender, MailKitMailSender>();
            else
                logger.LogWarning("Emails will NOT be sent in Dev mode.");

            if (Settings.Current.EnableRedis) {
                container.AddSingleton<ConnectionMultiplexer>(s => {
                    var multiplexer = ConnectionMultiplexer.Connect(Settings.Current.RedisConnectionString);
                    multiplexer.PreserveAsyncOrder = false;
                    return multiplexer;
                });

                if (Settings.Current.HasAppScope)
                    container.AddSingleton<ICacheClient>(s => new ScopedCacheClient(CreateRedisCacheClient(s, loggerFactory), Settings.Current.AppScope));
                else
                    container.AddSingleton<ICacheClient>(s => CreateRedisCacheClient(s, loggerFactory));

                if (Settings.Current.EnableWebSockets)
                    container.AddSingleton<IConnectionMapping, RedisConnectionMapping>();
                else
                    logger.LogWarning("Web Sockets is NOT enabled.");

                container.AddSingleton<IMessageBus>(s => new RedisMessageBus(new RedisMessageBusOptions {
                    Subscriber = s.GetRequiredService<ConnectionMultiplexer>().GetSubscriber(),
                    Topic = $"{Settings.Current.AppScopePrefix}messages",
                    Serializer = s.GetRequiredService<ISerializer>(),
                    LoggerFactory = loggerFactory
                }));
            } else {
                logger.LogWarning("Redis is NOT enabled.");
            }

            if (Settings.Current.EnableAzureStorage) {
                container.AddSingleton(s => CreateAzureStorageQueue<EventPost>(s, retries: 1, loggerFactory: loggerFactory));
                container.AddSingleton(s => CreateAzureStorageQueue<EventUserDescription>(s, loggerFactory: loggerFactory));
                container.AddSingleton(s => CreateAzureStorageQueue<EventNotificationWorkItem>(s, loggerFactory: loggerFactory));
                container.AddSingleton(s => CreateAzureStorageQueue<WebHookNotification>(s, loggerFactory: loggerFactory));
                container.AddSingleton(s => CreateAzureStorageQueue<MailMessage>(s, loggerFactory: loggerFactory));
                container.AddSingleton(s => CreateAzureStorageQueue<WorkItemData>(s, workItemTimeout: TimeSpan.FromHours(1), loggerFactory: loggerFactory));
            } else if (Settings.Current.EnableRedis) {
                container.AddSingleton(s => CreateRedisQueue<EventPost>(s, runMaintenanceTasks, retries: 1, loggerFactory: loggerFactory));
                container.AddSingleton(s => CreateRedisQueue<EventUserDescription>(s, runMaintenanceTasks, loggerFactory: loggerFactory));
                container.AddSingleton(s => CreateRedisQueue<EventNotificationWorkItem>(s, runMaintenanceTasks, loggerFactory: loggerFactory));
                container.AddSingleton(s => CreateRedisQueue<WebHookNotification>(s, runMaintenanceTasks, loggerFactory: loggerFactory));
                container.AddSingleton(s => CreateRedisQueue<MailMessage>(s, runMaintenanceTasks, loggerFactory: loggerFactory));
                container.AddSingleton(s => CreateRedisQueue<WorkItemData>(s, runMaintenanceTasks, workItemTimeout: TimeSpan.FromHours(1), loggerFactory: loggerFactory));
            }

            if (Settings.Current.EnableAzureStorage)
                container.AddSingleton<IFileStorage>(s => new AzureFileStorage(Settings.Current.AzureStorageConnectionString, $"{Settings.Current.AppScopePrefix}ex-events"));
            else
                logger.LogWarning("Azure Storage is NOT enabled.");
        }

        private static IQueue<T> CreateAzureStorageQueue<T>(IServiceProvider container, int retries = 2, TimeSpan? workItemTimeout = null, ILoggerFactory loggerFactory = null) where T : class {
            return new AzureStorageQueue<T>(new AzureStorageQueueOptions<T> {
                ConnectionString = Settings.Current.AzureStorageConnectionString,
                Name = GetQueueName<T>().ToLowerInvariant(),
                Retries = retries,
                Behaviors = container.GetServices<IQueueBehavior<T>>().ToList(),
                WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
                Serializer = container.GetRequiredService<ISerializer>(),
                LoggerFactory = loggerFactory
            });
        }

        private static IQueue<T> CreateRedisQueue<T>(IServiceProvider container, bool runMaintenanceTasks, int retries = 2, TimeSpan? workItemTimeout = null, ILoggerFactory loggerFactory = null) where T : class {
            return new RedisQueue<T>(new RedisQueueOptions<T> {
                ConnectionMultiplexer = container.GetRequiredService<ConnectionMultiplexer>(),
                Name = GetQueueName<T>(),
                Retries = retries,
                Behaviors = container.GetServices<IQueueBehavior<T>>().ToList(),
                WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
                RunMaintenanceTasks = runMaintenanceTasks,
                Serializer = container.GetRequiredService<ISerializer>(),
                LoggerFactory = loggerFactory
            });
        }

        private static string GetQueueName<T>() {
            return String.Concat(Settings.Current.AppScopePrefix, typeof(T).Name);
        }

        private static RedisCacheClient CreateRedisCacheClient(IServiceProvider container, ILoggerFactory loggerFactory) {
            return new RedisCacheClient(new RedisCacheClientOptions {
                ConnectionMultiplexer = container.GetRequiredService<ConnectionMultiplexer>(),
                Serializer = container.GetRequiredService<ISerializer>(),
                LoggerFactory = loggerFactory
            });
        }
    }
}