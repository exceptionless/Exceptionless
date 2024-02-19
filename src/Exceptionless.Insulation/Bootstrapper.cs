using Amazon;
using Amazon.Runtime;
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
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Serializer;
using Foundatio.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Serilog.Sinks.Exceptionless;
using StackExchange.Redis;
using QueueOptions = Exceptionless.Core.Configuration.QueueOptions;

namespace Exceptionless.Insulation;

public class Bootstrapper
{
    public static void RegisterServices(IServiceCollection services, AppOptions appOptions, bool runMaintenanceTasks)
    {
        if (!String.IsNullOrEmpty(appOptions.ExceptionlessApiKey) && !String.IsNullOrEmpty(appOptions.ExceptionlessServerUrl))
        {
            var client = ExceptionlessClient.Default;
            client.Configuration.ServerUrl = appOptions.ExceptionlessServerUrl;
            client.Configuration.ApiKey = appOptions.ExceptionlessApiKey;

            client.Configuration.SetDefaultMinLogLevel(Logging.LogLevel.Warn);
            client.Configuration.UseLogger(new SelfLogLogger());

            if (!String.IsNullOrEmpty(appOptions.Version))
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
        RegisterQueue(services, appOptions.QueueOptions, runMaintenanceTasks);
        RegisterStorage(services, appOptions.StorageOptions);

        var healthCheckBuilder = RegisterHealthChecks(services);

        if (!String.IsNullOrEmpty(appOptions.EmailOptions.SmtpHost))
        {
            services.ReplaceSingleton<IMailSender, MailKitMailSender>();
            healthCheckBuilder.Add(new HealthCheckRegistration("Mail", s => (MailKitMailSender)s.GetRequiredService<IMailSender>(), null, ["Mail", "MailMessage", "AllJobs"]));
        }
    }

    private static IHealthChecksBuilder RegisterHealthChecks(IServiceCollection services)
    {
        services.AddStartupActionToWaitForHealthChecks("Critical");

        return services.AddHealthChecks()
            .AddCheckForStartupActions("Critical")

            .AddAutoNamedCheck<ElasticsearchHealthCheck>("Critical")
            .AddAutoNamedCheck<CacheHealthCheck>("Critical")
            //.AddAutoNamedCheck<StorageHealthCheck>("EventPosts", "AllJobs")

            .AddAutoNamedCheck<QueueHealthCheck<EventPost>>("EventPosts", "AllJobs")
            .AddAutoNamedCheck<QueueHealthCheck<EventUserDescription>>("EventUserDescriptions", "AllJobs")
            .AddAutoNamedCheck<QueueHealthCheck<EventNotification>>("EventNotifications", "AllJobs")
            .AddAutoNamedCheck<QueueHealthCheck<WebHookNotification>>("WebHooks", "AllJobs")
            .AddAutoNamedCheck<QueueHealthCheck<MailMessage>>("AllJobs")
            .AddAutoNamedCheck<QueueHealthCheck<WorkItemData>>("WorkItem", "AllJobs")

            .AddAutoNamedCheck<CloseInactiveSessionsJob>("AllJobs")
            .AddAutoNamedCheck<DailySummaryJob>("AllJobs")
            .AddAutoNamedCheck<DownloadGeoIPDatabaseJob>("AllJobs")
            .AddAutoNamedCheck<MaintainIndexesJob>("AllJobs")
            .AddAutoNamedCheck<CleanupDataJob>("AllJobs")
            .AddAutoNamedCheck<StackStatusJob>("AllJobs")
            .AddAutoNamedCheck<StackEventCountJob>("AllJobs");
    }

    private static void RegisterCache(IServiceCollection container, CacheOptions options)
    {
        if (String.Equals(options.Provider, "redis"))
        {
            container.ReplaceSingleton(s => GetRedisConnection(options.Data, s.GetRequiredService<ILoggerFactory>()));

            if (!String.IsNullOrEmpty(options.Scope))
                container.ReplaceSingleton<ICacheClient>(s => new ScopedCacheClient(CreateRedisCacheClient(s), options.Scope));
            else
                container.ReplaceSingleton<ICacheClient>(CreateRedisCacheClient);

            container.ReplaceSingleton<IConnectionMapping, RedisConnectionMapping>();
        }
    }

    private static void RegisterMessageBus(IServiceCollection container, MessageBusOptions options)
    {
        if (String.Equals(options.Provider, "redis"))
        {
            container.ReplaceSingleton(s => GetRedisConnection(options.Data, s.GetRequiredService<ILoggerFactory>()));

            container.ReplaceSingleton<IMessageBus>(s => new RedisMessageBus(new RedisMessageBusOptions
            {
                Subscriber = s.GetRequiredService<IConnectionMultiplexer>().GetSubscriber(),
                Topic = options.Topic,
                Serializer = s.GetRequiredService<ISerializer>(),
                LoggerFactory = s.GetRequiredService<ILoggerFactory>()
            }));
        }
        else if (String.Equals(options.Provider, "rabbitmq"))
        {
            container.ReplaceSingleton<IMessageBus>(s => new RabbitMQMessageBus(new RabbitMQMessageBusOptions
            {
                ConnectionString = options.ConnectionString,
                Topic = options.Topic,
                Serializer = s.GetRequiredService<ISerializer>(),
                LoggerFactory = s.GetRequiredService<ILoggerFactory>()
            }));
        }
    }

    private static IConnectionMultiplexer GetRedisConnection(Dictionary<string, string> options, ILoggerFactory loggerFactory)
    {
        return ConnectionMultiplexer.Connect(options.GetString("server"), o => o.LoggerFactory = loggerFactory);
    }

    private static void RegisterQueue(IServiceCollection container, QueueOptions options, bool runMaintenanceTasks)
    {
        if (String.Equals(options.Provider, "azurestorage"))
        {
            container.ReplaceSingleton(s => CreateAzureStorageQueue<EventPost>(s, options, retries: 1));
            container.ReplaceSingleton(s => CreateAzureStorageQueue<EventUserDescription>(s, options));
            container.ReplaceSingleton(s => CreateAzureStorageQueue<EventNotification>(s, options));
            container.ReplaceSingleton(s => CreateAzureStorageQueue<WebHookNotification>(s, options));
            container.ReplaceSingleton(s => CreateAzureStorageQueue<MailMessage>(s, options));
            container.ReplaceSingleton(s => CreateAzureStorageQueue<WorkItemData>(s, options, workItemTimeout: TimeSpan.FromHours(1)));
        }
        else if (String.Equals(options.Provider, "redis"))
        {
            container.ReplaceSingleton(s => CreateRedisQueue<EventPost>(s, options, runMaintenanceTasks, retries: 1));
            container.ReplaceSingleton(s => CreateRedisQueue<EventUserDescription>(s, options, runMaintenanceTasks));
            container.ReplaceSingleton(s => CreateRedisQueue<EventNotification>(s, options, runMaintenanceTasks));
            container.ReplaceSingleton(s => CreateRedisQueue<WebHookNotification>(s, options, runMaintenanceTasks));
            container.ReplaceSingleton(s => CreateRedisQueue<MailMessage>(s, options, runMaintenanceTasks));
            container.ReplaceSingleton(s => CreateRedisQueue<WorkItemData>(s, options, runMaintenanceTasks, workItemTimeout: TimeSpan.FromHours(1)));
        }
        else if (String.Equals(options.Provider, "sqs"))
        {
            container.ReplaceSingleton(s => CreateSQSQueue<EventPost>(s, options, retries: 1));
            container.ReplaceSingleton(s => CreateSQSQueue<EventUserDescription>(s, options));
            container.ReplaceSingleton(s => CreateSQSQueue<EventNotification>(s, options));
            container.ReplaceSingleton(s => CreateSQSQueue<WebHookNotification>(s, options));
            container.ReplaceSingleton(s => CreateSQSQueue<MailMessage>(s, options));
            container.ReplaceSingleton(s => CreateSQSQueue<WorkItemData>(s, options, workItemTimeout: TimeSpan.FromHours(1)));
        }
    }

    private static void RegisterStorage(IServiceCollection container, StorageOptions options)
    {
        if (String.Equals(options.Provider, "aliyun"))
        {
            container.ReplaceSingleton<IFileStorage>(s => new AliyunFileStorage(new AliyunFileStorageOptions
            {
                ConnectionString = options.ConnectionString,
                Serializer = s.GetRequiredService<ITextSerializer>(),
                LoggerFactory = s.GetRequiredService<ILoggerFactory>()
            }));
        }
        else if (String.Equals(options.Provider, "azurestorage"))
        {
            container.ReplaceSingleton<IFileStorage>(s => new AzureFileStorage(new AzureFileStorageOptions
            {
                ConnectionString = options.ConnectionString,
                ContainerName = $"{options.ScopePrefix}ex-events",
                Serializer = s.GetRequiredService<ITextSerializer>(),
                LoggerFactory = s.GetRequiredService<ILoggerFactory>()
            }));
        }
        else if (String.Equals(options.Provider, "folder"))
        {
            string path = options.Data.GetString("path", "|DataDirectory|\\storage");
            container.AddSingleton<IFileStorage>(s => new FolderFileStorage(new FolderFileStorageOptions
            {
                Folder = PathHelper.ExpandPath(path),
                Serializer = s.GetRequiredService<ITextSerializer>(),
                LoggerFactory = s.GetRequiredService<ILoggerFactory>()
            }));
        }
        else if (String.Equals(options.Provider, "minio"))
        {
            container.ReplaceSingleton<IFileStorage>(s => new MinioFileStorage(new MinioFileStorageOptions
            {
                ConnectionString = options.ConnectionString,
                Serializer = s.GetRequiredService<ITextSerializer>(),
                LoggerFactory = s.GetRequiredService<ILoggerFactory>()
            }));
        }
        else if (String.Equals(options.Provider, "s3"))
        {
            container.ReplaceSingleton<IFileStorage>(s => new S3FileStorage(new S3FileStorageOptions
            {
                ConnectionString = options.ConnectionString,
                Credentials = GetAWSCredentials(options.Data),
                Region = GetAWSRegionEndpoint(options.Data),
                Bucket = $"{options.ScopePrefix}{options.Data.GetString("bucket", "ex-events")}",
                Serializer = s.GetRequiredService<ITextSerializer>(),
                LoggerFactory = s.GetRequiredService<ILoggerFactory>()
            }));
        }
    }

    private static IQueue<T> CreateAzureStorageQueue<T>(IServiceProvider container, QueueOptions options, int retries = 2, TimeSpan? workItemTimeout = null) where T : class
    {
        return new AzureStorageQueue<T>(new AzureStorageQueueOptions<T>
        {
            ConnectionString = options.ConnectionString,
            Name = GetQueueName<T>(options).ToLowerInvariant(),
            Retries = retries,
            Behaviors = container.GetServices<IQueueBehavior<T>>().ToList(),
            WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
            Serializer = container.GetRequiredService<ISerializer>(),
            LoggerFactory = container.GetRequiredService<ILoggerFactory>()
        });
    }

    private static IQueue<T> CreateRedisQueue<T>(IServiceProvider container, QueueOptions options, bool runMaintenanceTasks, int retries = 2, TimeSpan? workItemTimeout = null) where T : class
    {
        return new RedisQueue<T>(new RedisQueueOptions<T>
        {
            ConnectionMultiplexer = container.GetRequiredService<IConnectionMultiplexer>(),
            Name = GetQueueName<T>(options),
            Retries = retries,
            Behaviors = container.GetServices<IQueueBehavior<T>>().ToList(),
            WorkItemTimeout = workItemTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5.0)),
            RunMaintenanceTasks = runMaintenanceTasks,
            Serializer = container.GetRequiredService<ISerializer>(),
            LoggerFactory = container.GetRequiredService<ILoggerFactory>()
        });
    }

    private static RedisCacheClient CreateRedisCacheClient(IServiceProvider container)
    {
        return new RedisCacheClient(new RedisCacheClientOptions
        {
            ConnectionMultiplexer = container.GetRequiredService<IConnectionMultiplexer>(),
            Serializer = container.GetRequiredService<ISerializer>(),
            LoggerFactory = container.GetRequiredService<ILoggerFactory>()
        });
    }

    private static IQueue<T> CreateSQSQueue<T>(IServiceProvider container, QueueOptions options, int retries = 2, TimeSpan? workItemTimeout = null) where T : class
    {
        return new SQSQueue<T>(new SQSQueueOptions<T>
        {
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

    private static string GetQueueName<T>(QueueOptions options)
    {
        return String.Concat(options.ScopePrefix, typeof(T).Name);
    }

    private static RegionEndpoint GetAWSRegionEndpoint(IDictionary<string, string> data)
    {
        string region = data.GetString("region");
        return RegionEndpoint.GetBySystemName(String.IsNullOrEmpty(region) ? "us-east-1" : region);
    }

    private static AWSCredentials GetAWSCredentials(IDictionary<string, string> data)
    {
        string accessKey = data.GetString("accesskey");
        string secretKey = data.GetString("secretkey");
        if (String.IsNullOrEmpty(accessKey)
            || String.IsNullOrEmpty(secretKey))
            return FallbackCredentialsFactory.GetCredentials();

        return new BasicAWSCredentials(accessKey, secretKey);
    }
}
