using System;
using System.Linq;
using App.Metrics;
using App.Metrics.Infrastructure;
using App.Metrics.Internal.Infrastructure;
using App.Metrics.Reporting.Graphite;
using App.Metrics.Reporting.Http;
using App.Metrics.Reporting.InfluxDB;
using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Sinks.Exceptionless;
using StackExchange.Redis;

namespace Exceptionless.Insulation {
    public class Bootstrapper {
        public static void RegisterServices(IServiceProvider serviceProvider, IServiceCollection services, AppOptions appOptions, bool runMaintenanceTasks) {
            if (!String.IsNullOrEmpty(appOptions.ExceptionlessApiKey) && !String.IsNullOrEmpty(appOptions.ExceptionlessServerUrl)) {
                var client = ExceptionlessClient.Default;
                client.Configuration.ServerUrl = appOptions.ExceptionlessServerUrl;
                client.Configuration.ApiKey = appOptions.ExceptionlessApiKey;

                client.Configuration.SetDefaultMinLogLevel(Logging.LogLevel.Warn);
                client.Configuration.UseLogger(new SelfLogLogger());
                client.Configuration.SetVersion(appOptions.Version);
                if (String.IsNullOrEmpty(appOptions.InternalProjectId))
                    client.Configuration.Enabled = false;

                client.Configuration.UseInMemoryStorage();
                client.Configuration.UseReferenceIds();

                services.ReplaceSingleton<ICoreLastReferenceIdManager, ExceptionlessClientCoreLastReferenceIdManager>();
                services.AddSingleton<ExceptionlessClient>(client);
            }

            if (!String.IsNullOrEmpty(appOptions.GoogleGeocodingApiKey))
                services.ReplaceSingleton<IGeocodeService>(s => new GoogleGeocodeService(appOptions.GoogleGeocodingApiKey));

            RegisterCache(services, serviceProvider.GetRequiredService<IOptions<CacheOptions>>().Value, appOptions);
            RegisterMessageBus(services, serviceProvider.GetRequiredService<IOptions<MessageBusOptions>>().Value, appOptions);
            RegisterMetric(services, serviceProvider.GetRequiredService<IOptions<MetricOptions>>().Value);
            RegisterQueue(services, serviceProvider.GetRequiredService<IOptions<QueueOptions>>().Value, runMaintenanceTasks);
            RegisterStorage(services, serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value, appOptions);

            if (appOptions.AppMode != AppMode.Development)
                services.ReplaceSingleton<IMailSender, MailKitMailSender>();
        }

        private static void RegisterCache(IServiceCollection container, CacheOptions options, AppOptions appOptions) {
            if (String.Equals(options.Provider, "redis")) {
                container.AddSingleton<ConnectionMultiplexer>(s => ConnectionMultiplexer.Connect(options.ConnectionString));

                if (appOptions.HasScope)
                    container.ReplaceSingleton<ICacheClient>(s => new ScopedCacheClient(CreateRedisCacheClient(s), appOptions.Scope));
                else
                    container.ReplaceSingleton<ICacheClient>(CreateRedisCacheClient);

                container.ReplaceSingleton<IConnectionMapping, RedisConnectionMapping>();
            }
        }

        private static void RegisterMessageBus(IServiceCollection container, MessageBusOptions options, AppOptions appOptions) {
            if (String.Equals(options.Provider, "redis")) {
                container.AddSingleton<ConnectionMultiplexer>(s => ConnectionMultiplexer.Connect(options.ConnectionString));

                container.ReplaceSingleton<IMessageBus>(s => new RedisMessageBus(new RedisMessageBusOptions {
                    Subscriber = s.GetRequiredService<ConnectionMultiplexer>().GetSubscriber(),
                    Topic = $"{appOptions.ScopePrefix}messages",
                    Serializer = s.GetRequiredService<ISerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            }
        }

        private static void RegisterMetric(IServiceCollection container, MetricOptions options) {
            if (String.Equals(options.Provider, "statsd")) {
                container.ReplaceSingleton<IMetricsClient>(s => new StatsDMetricsClient(new StatsDMetricsClientOptions {
                    ServerName = options.Data.GetString("server", "127.0.0.1"),
                    Port = options.Data.GetValueOrDefault("port", 8125),
                    Prefix = "ex",
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            } else {
                var metrics = BuildAppMetrics(options);
                if (metrics == null)
                    return;

                container.ReplaceSingleton(metrics.Clock);
                container.ReplaceSingleton(metrics.Filter);
                container.ReplaceSingleton(metrics.DefaultOutputMetricsFormatter);
                container.ReplaceSingleton(metrics.OutputMetricsFormatters);
                container.ReplaceSingleton(metrics.DefaultOutputEnvFormatter);
                container.ReplaceSingleton(metrics.OutputEnvFormatters);
                container.TryAddSingleton<EnvironmentInfoProvider>();
                container.ReplaceSingleton<IMetrics>(metrics);
                container.ReplaceSingleton(metrics);
                container.ReplaceSingleton(metrics.Options);
                container.ReplaceSingleton(metrics.Reporters);
                container.ReplaceSingleton(metrics.ReportRunner);
                container.TryAddSingleton<AppMetricsMarkerService, AppMetricsMarkerService>();
                container.ReplaceSingleton<IMetricsClient, AppMetricsClient>();
            }
        }

        private static IMetricsRoot BuildAppMetrics(MetricOptions options) {
            var metricsBuilder = AppMetrics.CreateDefaultBuilder();
            switch (options.Provider) {
                case "influxdb":
                    metricsBuilder.Report.ToInfluxDb(new MetricsReportingInfluxDbOptions {
                        InfluxDb = {
                            BaseUri = new Uri(options.Data.GetString("server")),
                            UserName = options.Data.GetString("username"),
                            Password = options.Data.GetString("password"),
                            Database = options.Data.GetString("database", "exceptionless")
                        }
                    });
                    break;
                case "http":
                    metricsBuilder.Report.OverHttp(new MetricsReportingHttpOptions {
                        HttpSettings = {
                            RequestUri = new Uri(options.Data.GetString("server")),
                            UserName = options.Data.GetString("username"),
                            Password = options.Data.GetString("password"),
                        }
                    });
                    break;
                case "graphite":
                    metricsBuilder.Report.ToGraphite(new MetricsReportingGraphiteOptions {
                        Graphite = {
                            BaseUri = new Uri(options.Data.GetString("server"))
                        }
                    });
                    break;
                default:
                    return null;
            }
            
            return metricsBuilder.Build();
        }

        private static void RegisterQueue(IServiceCollection container, QueueOptions options, bool runMaintenanceTasks) {
            if (String.Equals(options.Provider, "redis")) {
                container.ReplaceSingleton(s => CreateRedisQueue<EventPost>(s, options, runMaintenanceTasks, retries: 1));
                container.ReplaceSingleton(s => CreateRedisQueue<EventUserDescription>(s, options, runMaintenanceTasks));
                container.ReplaceSingleton(s => CreateRedisQueue<EventNotificationWorkItem>(s, options, runMaintenanceTasks));
                container.ReplaceSingleton(s => CreateRedisQueue<WebHookNotification>(s, options, runMaintenanceTasks));
                container.ReplaceSingleton(s => CreateRedisQueue<MailMessage>(s, options, runMaintenanceTasks));
                container.ReplaceSingleton(s => CreateRedisQueue<WorkItemData>(s, options, runMaintenanceTasks, workItemTimeout: TimeSpan.FromHours(1)));
            } else if (String.Equals(options.Provider, "azurestorage")) {
                container.ReplaceSingleton(s => CreateAzureStorageQueue<EventPost>(s, options, retries: 1));
                container.ReplaceSingleton(s => CreateAzureStorageQueue<EventUserDescription>(s, options));
                container.ReplaceSingleton(s => CreateAzureStorageQueue<EventNotificationWorkItem>(s, options));
                container.ReplaceSingleton(s => CreateAzureStorageQueue<WebHookNotification>(s, options));
                container.ReplaceSingleton(s => CreateAzureStorageQueue<MailMessage>(s, options));
                container.ReplaceSingleton(s => CreateAzureStorageQueue<WorkItemData>(s, options, workItemTimeout: TimeSpan.FromHours(1)));
            }
        }

        private static void RegisterStorage(IServiceCollection container, StorageOptions options, AppOptions appOptions) {
            if (String.Equals(options.Provider, "folder")) {
                string path = options.Data.GetString("path", "|DataDirectory|\\storage");
                container.AddSingleton<IFileStorage>(s => new FolderFileStorage(new FolderFileStorageOptions {
                    Folder = PathHelper.ExpandPath(path),
                    Serializer = s.GetRequiredService<ITextSerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            } else if (String.Equals(options.Provider, "azurestorage")) {
                container.ReplaceSingleton<IFileStorage>(s => new AzureFileStorage(new AzureFileStorageOptions {
                    ConnectionString = options.ConnectionString,
                    ContainerName = $"{appOptions.ScopePrefix}ex-events",
                    Serializer = s.GetRequiredService<ITextSerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            } else if (String.Equals(options.Provider, "aliyun")) {
                container.ReplaceSingleton<IFileStorage>(s => new AliyunFileStorage(new AliyunFileStorageOptions {
                    ConnectionString = options.ConnectionString,
                    Serializer = s.GetRequiredService<ITextSerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            } else if (String.Equals(options.Provider, "minio")) {
                container.ReplaceSingleton<IFileStorage>(s => new MinioFileStorage(new MinioFileStorageOptions {
                    ConnectionString = options.ConnectionString,
                    Serializer = s.GetRequiredService<ITextSerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            }
        }

        private static IQueue<T> CreateAzureStorageQueue<T>(IServiceProvider container, QueueOptions options, int retries = 2, TimeSpan? workItemTimeout = null) where T : class {
            return new AzureStorageQueue<T>(new AzureStorageQueueOptions<T> {
                ConnectionString = options.ConnectionString,
                Name = GetQueueName<T>(options).ToLowerInvariant(),
                Retries = retries,
                Behaviors = container.GetServices<IQueueBehavior<T>>().ToList(),
                WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
                Serializer = container.GetRequiredService<ISerializer>(),
                LoggerFactory = container.GetRequiredService<ILoggerFactory>()
            });
        }

        private static IQueue<T> CreateRedisQueue<T>(IServiceProvider container, QueueOptions options, bool runMaintenanceTasks, int retries = 2, TimeSpan? workItemTimeout = null) where T : class {
            return new RedisQueue<T>(new RedisQueueOptions<T> {
                ConnectionMultiplexer = container.GetRequiredService<ConnectionMultiplexer>(),
                Name = GetQueueName<T>(options),
                Retries = retries,
                Behaviors = container.GetServices<IQueueBehavior<T>>().ToList(),
                WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
                RunMaintenanceTasks = runMaintenanceTasks,
                Serializer = container.GetRequiredService<ISerializer>(),
                LoggerFactory = container.GetRequiredService<ILoggerFactory>()
            });
        }

        private static RedisCacheClient CreateRedisCacheClient(IServiceProvider container) {
            return new RedisCacheClient(new RedisCacheClientOptions {
                ConnectionMultiplexer = container.GetRequiredService<ConnectionMultiplexer>(),
                Serializer = container.GetRequiredService<ISerializer>(),
                LoggerFactory = container.GetRequiredService<ILoggerFactory>()
            });
        }

        private static string GetQueueName<T>(QueueOptions options) {
            return String.Concat(options.ScopePrefix, typeof(T).Name);
        }
    }
}