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
using Foundatio.ServiceProviders;

namespace Exceptionless.Functions {
    public class FunctionRunner {
        private static readonly ILoggerFactory _loggerFactory;
        private static readonly IServiceProvider _serviceProvider;

        static FunctionRunner() {
            AppDomain.CurrentDomain.SetDataDirectory();
            _loggerFactory = Settings.Current.GetLoggerFactory();
             _serviceProvider = ServiceProvider.GetServiceProvider(Settings.JobBootstrappedServiceProvider, _loggerFactory);
        }

        public static Task ProcessEventPostQueueItem(EventPost data, string id, DateTimeOffset insertionTime, int dequeueCount, CancellationToken token = default(CancellationToken)) {
            return ProcessQueueItem<EventPostsJob, EventPost>(id, data, insertionTime.UtcDateTime, dequeueCount, token);
        }

        private static Task ProcessQueueItem<TJob, TWorkItem>(string id, TWorkItem data, DateTime enqueuedTimeUtc, int attempts, CancellationToken token) where TJob : QueueJobBase<TWorkItem> where TWorkItem : class {
            var job = _serviceProvider.GetService<TJob>();
            return job.ProcessAsync(new QueueEntry<TWorkItem>(id, data, (IQueue<TWorkItem>)((IQueueJob)job).Queue, enqueuedTimeUtc, attempts), token);
        }
    }
}
