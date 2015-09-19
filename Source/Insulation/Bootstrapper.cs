using System;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Utility;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Serializer;
using Foundatio.Storage;
using NLog.Fluent;
using SimpleInjector;
using SimpleInjector.Packaging;
using StackExchange.Redis;

namespace Exceptionless.Insulation {
    public class Bootstrapper : IPackage {
        public async Task RegisterServices(Container container) {
            if (Settings.Current.EnableMetricsReporting)
                container.RegisterSingleton<IMetricsClient>(() => new StatsDMetricsClient(Settings.Current.MetricsServerName, Settings.Current.MetricsServerPort, "ex"));
            else
                Log.Warn().Message("StatsD Metrics is NOT enabled.").Write();

            if (Settings.Current.EnableRedis) {
                var muxer = await ConnectionMultiplexer.ConnectAsync(Settings.Current.RedisConnectionString).AnyContext();
                muxer.PreserveAsyncOrder = false;
                container.RegisterSingleton(muxer);

                container.RegisterSingleton<ICacheClient, RedisHybridCacheClient>();

                container.RegisterSingleton<IQueue<EventPost>>(() => new RedisQueue<EventPost>(muxer, container.GetInstance<ISerializer>(), behaviors: container.GetAllInstances<IQueueBehavior<EventPost>>()));
                container.RegisterSingleton<IQueue<EventUserDescription>>(() => new RedisQueue<EventUserDescription>(muxer, container.GetInstance<ISerializer>(), behaviors: container.GetAllInstances<IQueueBehavior<EventUserDescription>>()));
                container.RegisterSingleton<IQueue<EventNotificationWorkItem>>(() => new RedisQueue<EventNotificationWorkItem>(muxer, container.GetInstance<ISerializer>(), behaviors: container.GetAllInstances<IQueueBehavior<EventNotificationWorkItem>>()));
                container.RegisterSingleton<IQueue<WebHookNotification>>(() => new RedisQueue<WebHookNotification>(muxer, container.GetInstance<ISerializer>(), behaviors: container.GetAllInstances<IQueueBehavior<WebHookNotification>>()));
                container.RegisterSingleton<IQueue<MailMessage>>(() => new RedisQueue<MailMessage>(muxer, container.GetInstance<ISerializer>(), behaviors: container.GetAllInstances<IQueueBehavior<MailMessage>>()));
                container.RegisterSingleton<IQueue<StatusMessage>>(() => new RedisQueue<StatusMessage>(muxer, container.GetInstance<ISerializer>()));
                container.RegisterSingleton<IQueue<WorkItemData>>(() => new RedisQueue<WorkItemData>(muxer, workItemTimeout: TimeSpan.FromHours(1), behaviors: container.GetAllInstances<IQueueBehavior<WorkItemData>>()));

                container.RegisterSingleton<IMessageBus>(() => new RedisMessageBus(muxer.GetSubscriber(), serializer: container.GetInstance<ISerializer>()));
            } else {
                Log.Warn().Message("Redis is NOT enabled.").Write();
            }

            if (Settings.Current.EnableAzureStorage)
                container.RegisterSingleton<IFileStorage>(new AzureFileStorage(Settings.Current.AzureStorageConnectionString, "ex-events"));
            else
                Log.Warn().Message("Azure Storage is NOT enabled.").Write();

            var client = ExceptionlessClient.Default;
            container.RegisterSingleton<ICoreLastReferenceIdManager, ExceptionlessClientCoreLastReferenceIdManager>();
            container.RegisterSingleton<ExceptionlessClient>(() => client);

            client.Configuration.SetVersion(Settings.Current.Version);
            client.Register();
            container.AddBootstrapper<HttpConfiguration>(config => client.RegisterWebApi(config));
            client.Configuration.UseInMemoryStorage();
            client.Configuration.UseReferenceIds();
        }
    }
}