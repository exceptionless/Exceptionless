using System;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Insulation.Geo;
using Exceptionless.Insulation.Logging;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Logging;
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
        public void RegisterServices(Container container) {
            Logger.RegisterWriter(new NLogAdapter());
            
            if (!String.IsNullOrEmpty(Settings.Current.GoogleGeocodingApiKey))
                container.RegisterSingleton<IGeocodeService>(() => new GoogleGeocodeService(Settings.Current.GoogleGeocodingApiKey));
            
            if (Settings.Current.EnableMetricsReporting)
                container.RegisterSingleton<IMetricsClient>(() => new StatsDMetricsClient(Settings.Current.MetricsServerName, Settings.Current.MetricsServerPort, "ex"));
            else
                Log.Warn().Message("StatsD Metrics is NOT enabled.").Write();

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

                container.RegisterSingleton<IQueue<EventPost>>(() => new RedisQueue<EventPost>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<EventPost>(), behaviors: container.GetAllInstances<IQueueBehavior<EventPost>>()));
                container.RegisterSingleton<IQueue<EventUserDescription>>(() => new RedisQueue<EventUserDescription>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<EventUserDescription>(), behaviors: container.GetAllInstances<IQueueBehavior<EventUserDescription>>()));
                container.RegisterSingleton<IQueue<EventNotificationWorkItem>>(() => new RedisQueue<EventNotificationWorkItem>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<EventNotificationWorkItem>(), behaviors: container.GetAllInstances<IQueueBehavior<EventNotificationWorkItem>>()));
                container.RegisterSingleton<IQueue<WebHookNotification>>(() => new RedisQueue<WebHookNotification>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<WebHookNotification>(), behaviors: container.GetAllInstances<IQueueBehavior<WebHookNotification>>()));
                container.RegisterSingleton<IQueue<MailMessage>>(() => new RedisQueue<MailMessage>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<MailMessage>(), behaviors: container.GetAllInstances<IQueueBehavior<MailMessage>>()));
                container.RegisterSingleton<IQueue<WorkItemData>>(() => new RedisQueue<WorkItemData>(container.GetInstance<ConnectionMultiplexer>(), container.GetInstance<ISerializer>(), GetQueueName<WorkItemData>(), workItemTimeout: TimeSpan.FromHours(1), behaviors: container.GetAllInstances<IQueueBehavior<WorkItemData>>()));

                container.RegisterSingleton<IMessageBus>(() => new RedisMessageBus(container.GetInstance<ConnectionMultiplexer>().GetSubscriber(), $"{Settings.Current.AppScopePrefix}messages", container.GetInstance<ISerializer>()));
            } else {
                Log.Warn().Message("Redis is NOT enabled.").Write();
            }

            if (Settings.Current.EnableAzureStorage)
                container.RegisterSingleton<IFileStorage>(new AzureFileStorage(Settings.Current.AzureStorageConnectionString, $"{Settings.Current.AppScopePrefix}ex-events"));
            else
                Log.Warn().Message("Azure Storage is NOT enabled.").Write();

            var client = ExceptionlessClient.Default;
            container.RegisterSingleton<ICoreLastReferenceIdManager, ExceptionlessClientCoreLastReferenceIdManager>();
            container.RegisterSingleton<ExceptionlessClient>(() => client);

            client.Configuration.SetVersion(Settings.Current.Version);
            if (String.IsNullOrEmpty(Settings.Current.InternalProjectId))
                client.Configuration.Enabled = false;
            client.Register();
            container.AddBootstrapper<HttpConfiguration>(config => client.RegisterWebApi(config));
            client.Configuration.UseInMemoryStorage();
            client.Configuration.UseReferenceIds();
        }

        private string GetQueueName<T>() {
            return String.Concat(Settings.Current.AppScopePrefix, typeof(T).Name);
        }
    }
}
