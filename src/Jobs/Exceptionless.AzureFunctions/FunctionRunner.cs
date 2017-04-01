using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Queues.Models;
using Foundatio.Extensions;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Queues;
using Foundatio.Serializer;
using Foundatio.ServiceProviders;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Exceptionless.AzureFunctions {
    public class JobRunner {
        private static readonly ILoggerFactory _loggerFactory;
        private static readonly IServiceProvider _serviceProvider;
        private static readonly ISerializer _serializer;

        static JobRunner() {
            AppDomain.CurrentDomain.SetDataDirectory();
            _loggerFactory = Settings.Current.GetLoggerFactory();
             _serviceProvider = ServiceProvider.GetServiceProvider(Settings.JobBootstrappedServiceProvider, _loggerFactory);
            _serializer = _serviceProvider.GetService<ISerializer>();
        }

        public static Task ProcessEventPostQueueItem(CloudQueueMessage message, CancellationToken token = default(CancellationToken)) {
            return ProcessQueueItem<EventPostsJob, EventPost>(message, token);
        }

        private static async Task ProcessQueueItem<TJob, TWorkItem>(CloudQueueMessage message, CancellationToken token) where TJob : QueueJobBase<TWorkItem> where TWorkItem : class {
            var job = _serviceProvider.GetService<TJob>();
            var data = await _serializer.DeserializeAsync<TWorkItem>(message.AsBytes).AnyContext();
            await job.ProcessAsync(new AzureStorageQueueEntry<TWorkItem>(message, data, (IQueue<TWorkItem>)((IQueueJob)job).Queue), token).AnyContext();
        }
    }
}
