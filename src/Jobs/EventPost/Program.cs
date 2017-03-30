using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues.Models;
using Foundatio.Extensions;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Queues;
using Foundatio.ServiceProviders;

namespace EventPostsJob {
    public class Program {
        private static readonly ILoggerFactory _loggerFactory;
        private static readonly Exceptionless.Core.Jobs.EventPostsJob _job;

        static Program() {
            AppDomain.CurrentDomain.SetDataDirectory();
            _loggerFactory = Settings.Current.GetLoggerFactory();

            var serviceProvider = ServiceProvider.GetServiceProvider(Settings.JobBootstrappedServiceProvider, _loggerFactory);
            _job = serviceProvider.GetService<Exceptionless.Core.Jobs.EventPostsJob>();
        }

        public static int Main() {
            return new JobRunner(_job, _loggerFactory, initialDelay: TimeSpan.FromSeconds(2), interval: TimeSpan.Zero).RunInConsole();
        }

        public static Task RunAsync(string id, EventPost data, DateTimeOffset insertionTime, int dequeueCount, CancellationToken token = default(CancellationToken)) {
            return _job.ProcessAsync(new QueueEntry<EventPost>(id, data, (IQueue<EventPost>)((IQueueJob)_job).Queue, insertionTime.UtcDateTime, dequeueCount), token);
        }
    }
}