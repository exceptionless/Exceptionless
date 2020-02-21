using System;
using System.Collections.Generic;
using System.Linq;
using Amazon;
using Amazon.Runtime;
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
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Jobs.Elastic;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Insulation.Geo;
using Exceptionless.Insulation.HealthChecks;
using Exceptionless.Insulation.Mail;
using Exceptionless.Insulation.Redis;
using Foundatio.Caching;
using Foundatio.Hosting.Startup;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Serializer;
using Foundatio.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Serilog.Sinks.Exceptionless;
using StackExchange.Redis;
using QueueOptions = Exceptionless.Core.Configuration.QueueOptions;

namespace Exceptionless.Insulation {
    public class Bootstrapper {
        public static void RegisterServices(IServiceCollection services, AppOptions appOptions, bool runMaintenanceTasks) {
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

            if (!String.IsNullOrEmpty(appOptions.MaxMindGeoIpKey))
                services.ReplaceSingleton<IGeoIpService, MaxMindGeoIpService>();
            
            RegisterCache(services, appOptions.CacheOptions);
            RegisterMessageBus(services, appOptions.MessageBusOptions);
            RegisterMetric(services, appOptions.MetricOptions);
            RegisterQueue(services, appOptions.QueueOptions, runMaintenanceTasks);
            RegisterStorage(services, appOptions.StorageOptions);

            var healthCheckBuilder = RegisterHealthChecks(services, appOptions);

            if (appOptions.AppMode != AppMode.Development) {
                if (!String.IsNullOrEmpty(appOptions.EmailOptions.SmtpHost)) {
                    services.ReplaceSingleton<IMailSender, MailKitMailSender>();
                    healthCheckBuilder.Add(new HealthCheckRegistration("Mail", s => s.GetRequiredService<IMailSender>() as MailKitMailSender, null, new[] { "Mail", "MailMessage", "AllJobs" }));
                }
            }
        }

        private static IHealthChecksBuilder RegisterHealthChecks(IServiceCollection services, AppOptions appOptions) {
            services.AddStartupActionToWaitForHealthChecks("Critical");

            return services.AddHealthChecks()
                .AddCheckForStartupActions("Critical")

                .AddAutoNamedCheck<ElasticsearchHealthCheck>("Critical")
                .AddAutoNamedCheck<CacheHealthCheck>("Critical")
                .AddAutoNamedCheck<StorageHealthCheck>("EventPosts", "AllJobs")
                
                .AddAutoNamedCheck<QueueHealthCheck<EventPost>>("EventPosts", "AllJobs")
                .AddAutoNamedCheck<QueueHealthCheck<EventUserDescription>>("EventUserDescriptions", "AllJobs")
                .AddAutoNamedCheck<QueueHealthCheck<EventNotificationWorkItem>>("EventNotifications", "AllJobs")
                .AddAutoNamedCheck<QueueHealthCheck<WebHookNotification>>("WebHooks", "AllJobs")
                .AddAutoNamedCheck<QueueHealthCheck<MailMessage>>("AllJobs")
                .AddAutoNamedCheck<QueueHealthCheck<WorkItemData>>("WorkItem", "AllJobs")

                .AddAutoNamedCheck<CloseInactiveSessionsJob>("AllJobs")
                .AddAutoNamedCheck<DailySummaryJob>("AllJobs")
                .AddAutoNamedCheck<DownloadGeoIPDatabaseJob>("AllJobs")
                .AddAutoNamedCheck<MaintainIndexesJob>("AllJobs")
                .AddAutoNamedCheck<RetentionLimitsJob>("AllJobs")
                .AddAutoNamedCheck<StackEventCountJob>("AllJobs");
        }

        private static void RegisterCache(IServiceCollection container, CacheOptions options) {
            if (String.Equals(options.Provider, "redis")) {
                container.AddSingleton<ConnectionMultiplexer>(s => ConnectionMultiplexer.Connect(options.Data.GetString("server")));

                if (!String.IsNullOrEmpty(options.Scope))
                    container.ReplaceSingleton<ICacheClient>(s => new ScopedCacheClient(CreateRedisCacheClient(s), options.Scope));
                else
                    container.ReplaceSingleton<ICacheClient>(CreateRedisCacheClient);

                container.ReplaceSingleton<IConnectionMapping, RedisConnectionMapping>();
            }
        }

        private static void RegisterMessageBus(IServiceCollection container, MessageBusOptions options) {
            if (String.Equals(options.Provider, "redis")) {
                container.AddSingleton<ConnectionMultiplexer>(s => ConnectionMultiplexer.Connect(options.Data.GetString("server")));

                container.ReplaceSingleton<IMessageBus>(s => new RedisMessageBus(new RedisMessageBusOptions {
                    Subscriber = s.GetRequiredService<ConnectionMultiplexer>().GetSubscriber(),
                    Topic = options.Topic,
                    Serializer = s.GetRequiredService<ISerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            } else if (String.Equals(options.Provider, "rabbitmq")) {
                container.ReplaceSingleton<IMessageBus>(s => new RabbitMQMessageBus(new RabbitMQMessageBusOptions {
                    ConnectionString = options.ConnectionString,
                    Topic = options.Topic,
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
                case "graphite":
                    metricsBuilder.Report.ToGraphite(new MetricsReportingGraphiteOptions {
                        Graphite = {
                            BaseUri = new Uri(options.Data.GetString("server"))
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
                default:
                    return null;
            }
            
            return metricsBuilder.Build();
        }

        private static void RegisterQueue(IServiceCollection container, QueueOptions options, bool runMaintenanceTasks) {
            if (String.Equals(options.Provider, "azurestorage")) {
                container.ReplaceSingleton(s => CreateAzureStorageQueue<EventPost>(s, options, retries: 1));
                container.ReplaceSingleton(s => CreateAzureStorageQueue<EventUserDescription>(s, options));
                container.ReplaceSingleton(s => CreateAzureStorageQueue<EventNotificationWorkItem>(s, options));
                container.ReplaceSingleton(s => CreateAzureStorageQueue<WebHookNotification>(s, options));
                container.ReplaceSingleton(s => CreateAzureStorageQueue<MailMessage>(s, options));
                container.ReplaceSingleton(s => CreateAzureStorageQueue<WorkItemData>(s, options, workItemTimeout: TimeSpan.FromHours(1)));
            } else if (String.Equals(options.Provider, "redis")) {
                container.ReplaceSingleton(s => CreateRedisQueue<EventPost>(s, options, runMaintenanceTasks, retries: 1));
                container.ReplaceSingleton(s => CreateRedisQueue<EventUserDescription>(s, options, runMaintenanceTasks));
                container.ReplaceSingleton(s => CreateRedisQueue<EventNotificationWorkItem>(s, options, runMaintenanceTasks));
                container.ReplaceSingleton(s => CreateRedisQueue<WebHookNotification>(s, options, runMaintenanceTasks));
                container.ReplaceSingleton(s => CreateRedisQueue<MailMessage>(s, options, runMaintenanceTasks));
                container.ReplaceSingleton(s => CreateRedisQueue<WorkItemData>(s, options, runMaintenanceTasks, workItemTimeout: TimeSpan.FromHours(1)));
            } else if (String.Equals(options.Provider, "sqs")) {
                container.ReplaceSingleton(s => CreateSQSQueue<EventPost>(s, options, retries: 1));
                container.ReplaceSingleton(s => CreateSQSQueue<EventUserDescription>(s, options));
                container.ReplaceSingleton(s => CreateSQSQueue<EventNotificationWorkItem>(s, options));
                container.ReplaceSingleton(s => CreateSQSQueue<WebHookNotification>(s, options));
                container.ReplaceSingleton(s => CreateSQSQueue<MailMessage>(s, options));
                container.ReplaceSingleton(s => CreateSQSQueue<WorkItemData>(s, options, workItemTimeout: TimeSpan.FromHours(1)));
            }
        }

        private static void RegisterStorage(IServiceCollection container, StorageOptions options) {
            if (String.Equals(options.Provider, "aliyun")) {
                container.ReplaceSingleton<IFileStorage>(s => new AliyunFileStorage(new AliyunFileStorageOptions {
                    ConnectionString = options.ConnectionString,
                    Serializer = s.GetRequiredService<ITextSerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            } else if (String.Equals(options.Provider, "azurestorage")) {
                container.ReplaceSingleton<IFileStorage>(s => new AzureFileStorage(new AzureFileStorageOptions {
                    ConnectionString = options.ConnectionString,
                    ContainerName = $"{options.ScopePrefix}ex-events",
                    Serializer = s.GetRequiredService<ITextSerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            } else if (String.Equals(options.Provider, "folder")) {
                string path = options.Data.GetString("path", "|DataDirectory|\\storage");
                container.AddSingleton<IFileStorage>(s => new FolderFileStorage(new FolderFileStorageOptions {
                    Folder = PathHelper.ExpandPath(path),
                    Serializer = s.GetRequiredService<ITextSerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            } else if (String.Equals(options.Provider, "minio")) {
                container.ReplaceSingleton<IFileStorage>(s => new MinioFileStorage(new MinioFileStorageOptions {
                    ConnectionString = options.ConnectionString,
                    Serializer = s.GetRequiredService<ITextSerializer>(),
                    LoggerFactory = s.GetRequiredService<ILoggerFactory>()
                }));
            } else if (String.Equals(options.Provider, "s3")) {
                 container.ReplaceSingleton<IFileStorage>(s => new S3FileStorage(new S3FileStorageOptions {
                    ConnectionString = options.ConnectionString,
                    Credentials = GetAWSCredentials(options.Data),
                    Region = GetAWSRegionEndpoint(options.Data),
                    Bucket = $"{options.ScopePrefix}{options.Data.GetString("bucket", "ex-events")}",
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

        private static IQueue<T> CreateSQSQueue<T>(IServiceProvider container, QueueOptions options, int retries = 2, TimeSpan? workItemTimeout = null) where T : class {
            return new SQSQueue<T>(new SQSQueueOptions<T> {
                Name = GetQueueName<T>(options),
                Credentials = GetAWSCredentials(options.Data),
                Region = GetAWSRegionEndpoint(options.Data),
                CanCreateQueue = false,
                Retries = retries,
                Behaviors = container.GetServices<IQueueBehavior<T>>().ToList(),
                WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
                Serializer = container.GetRequiredService<ISerializer>(),
                LoggerFactory = container.GetRequiredService<ILoggerFactory>()
            });
        }

        private static string GetQueueName<T>(QueueOptions options) {
            return String.Concat(options.ScopePrefix, typeof(T).Name);
        }
        
        private static RegionEndpoint GetAWSRegionEndpoint(IDictionary<string, string> data) {
            string region = data.GetString("region");
            return RegionEndpoint.GetBySystemName(String.IsNullOrEmpty(region) ? "us-east-1" : region);
        }

        private static AWSCredentials GetAWSCredentials(IDictionary<string, string> data) {
            string accessKey = data.GetString("accesskey");
            string secretKey = data.GetString("secretkey");
            if (String.IsNullOrEmpty(accessKey)
                || String.IsNullOrEmpty(secretKey))
                return FallbackCredentialsFactory.GetCredentials();

            return new BasicAWSCredentials(accessKey, secretKey);
        }
    }
}