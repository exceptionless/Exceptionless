using System;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Utility;
using Foundatio.AppStats;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Redis.Cache;
using Foundatio.Redis.Messaging;
using Foundatio.Redis.Queues;
using Foundatio.Serializer;
using Foundatio.Storage;
using SimpleInjector;
using SimpleInjector.Packaging;
using StackExchange.Redis;

namespace Exceptionless.Insulation {
    public class Bootstrapper : IPackage {
        public void RegisterServices(Container container) {
            if (Settings.Current.EnableMetricsReporting)
                container.RegisterSingle<IMetricsClient>(() => new StatsDMetricsClient(Settings.Current.MetricsServerName, Settings.Current.MetricsServerPort));

            if (Settings.Current.EnableRedis) {
                var muxer = ConnectionMultiplexer.Connect(Settings.Current.RedisConnectionString);
                container.RegisterSingle(muxer);

                container.RegisterSingle<ICacheClient, RedisHybridCacheClient>();

                container.RegisterSingle<IQueue<EventPost>>(() => new RedisQueue<EventPost>(muxer, container.GetInstance<ISerializer>(), statName: MetricNames.PostsQueueSize, metrics: container.GetInstance<IMetricsClient>()));
                container.RegisterSingle<IQueue<EventUserDescription>>(() => new RedisQueue<EventUserDescription>(muxer, container.GetInstance<ISerializer>(), statName: MetricNames.EventsUserDescriptionQueueSize, metrics: container.GetInstance<IMetricsClient>()));
                container.RegisterSingle<IQueue<EventNotificationWorkItem>>(() => new RedisQueue<EventNotificationWorkItem>(muxer, container.GetInstance<ISerializer>(), statName: MetricNames.EventNotificationQueueSize, metrics: container.GetInstance<IMetricsClient>()));
                container.RegisterSingle<IQueue<WebHookNotification>>(() => new RedisQueue<WebHookNotification>(muxer, container.GetInstance<ISerializer>(), statName: MetricNames.WebHookQueueSize, metrics: container.GetInstance<IMetricsClient>()));
                container.RegisterSingle<IQueue<MailMessage>>(() => new RedisQueue<MailMessage>(muxer, container.GetInstance<ISerializer>(), statName: MetricNames.EmailsQueueSize, metrics: container.GetInstance<IMetricsClient>()));

                container.RegisterSingle<IMessageBus>(() => new RedisMessageBus(muxer.GetSubscriber(), serializer: container.GetInstance<ISerializer>()));
            }

            if (Settings.Current.EnableAzureStorage)
                container.RegisterSingle<IFileStorage>(new AzureFileStorage(Settings.Current.AzureStorageConnectionString));

            var client = ExceptionlessClient.Default;
            container.RegisterSingle<ICoreLastReferenceIdManager, ExceptionlessClientCoreLastReferenceIdManager>();
            container.RegisterSingle<ExceptionlessClient>(() => client);

            client.Configuration.SetVersion(Settings.Current.Version);
            client.Register();
            container.AddBootstrapper<HttpConfiguration>(config => client.RegisterWebApi(config));
            client.Configuration.UseInMemoryStorage();
            client.Configuration.UseReferenceIds();
        }
    }
}