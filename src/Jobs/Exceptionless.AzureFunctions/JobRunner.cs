using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Jobs.Elastic;
using Exceptionless.Core.Queues.Models;
using Foundatio.Extensions;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Queues;
using Foundatio.Serializer;
using Foundatio.ServiceProviders;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
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

        public static Task RunCleanupSnapshotJob(TimerInfo timer, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return RunJob<CleanupSnapshotJob>(timer, log, token);
        }

        public static Task RunCloseInactiveSessionsJob(TimerInfo timer, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return RunJob<CloseInactiveSessionsJob>(timer, log, token);
        }

        public static Task RunDailySummaryJob(TimerInfo timer, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return RunJob<DailySummaryJob>(timer, log, token);
        }

        public static Task RunDownloadGeoIPDatabaseJob(TimerInfo timer, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return RunJob<DownloadGeoIPDatabaseJob>(timer, log, token);
        }

        public static Task RunEventSnapshotJob(TimerInfo timer, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return RunJob<EventSnapshotJob>(timer, log, token);
        }

        public static Task RunMaintainIndexesJob(TimerInfo timer, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return RunJob<MaintainIndexesJob>(timer, log, token);
        }

        public static Task RunOrganizationSnapshotJob(TimerInfo timer, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return RunJob<OrganizationSnapshotJob>(timer, log, token);
        }

        public static Task RunRetentionLimitsJob(TimerInfo timer, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return RunJob<RetentionLimitsJob>(timer, log, token);
        }

        public static Task RunStackSnapshotJob(TimerInfo timer, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return RunJob<StackSnapshotJob>(timer, log, token);
        }

        public static Task ProcessEventNotificationWorkItemQueueItem(CloudQueueMessage message, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return ProcessQueueItem<EventNotificationsJob, EventNotificationWorkItem>(message, log, token);
        }

        public static Task ProcessEventPostQueueItem(CloudQueueMessage message, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return ProcessQueueItem<EventPostsJob, EventPost>(message, log, token);
        }

        public static Task ProcessEventUserDescriptionQueueItem(CloudQueueMessage message, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return ProcessQueueItem<EventUserDescriptionsJob, EventUserDescription>(message, log, token);
        }

        public static Task ProcessMailMessageQueueItem(CloudQueueMessage message, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return ProcessQueueItem<MailMessageJob, MailMessage>(message, log, token);
        }

        public static Task ProcessWebHookNotificationQueueItem(CloudQueueMessage message, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return ProcessQueueItem<WebHooksJob, WebHookNotification>(message, log, token);
        }

        public static Task ProcessWorkItemDataQueueItem(CloudQueueMessage message, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return ProcessQueueItem<WorkItemJob, WorkItemData>(message, log, token);
        }

        private static async Task ProcessQueueItem<TJob, TWorkItem>(CloudQueueMessage message, TraceWriter log, CancellationToken token) where TJob : class, IQueueJob<TWorkItem> where TWorkItem : class {
            var job = _serviceProvider.GetService<TJob>();
            string jobName = typeof(TWorkItem).Name;

            log.Info($"Processing {jobName} queue item: {message.Id} Attempts: {message.DequeueCount} Enqueued: {message.InsertionTime.GetValueOrDefault():O}");
            var data = await _serializer.DeserializeAsync<TWorkItem>(message.AsBytes).AnyContext();
            var result = await job.ProcessAsync(new AzureStorageQueueEntry<TWorkItem>(message, data, job.Queue), token).AnyContext();
            LogResult(result, log, jobName);
        }

        private static async Task RunJob<TJob>(TimerInfo timer, TraceWriter log, CancellationToken token) where TJob : class, IJob {
            var job = _serviceProvider.GetService<TJob>();
            string jobName = job.GetType().Name;

            log.Info($"Running {jobName}");
            var result = await job.TryRunAsync(token).AnyContext();
            LogResult(result, log, jobName);
        }

        private static void LogResult(JobResult result, TraceWriter log, string jobName) {
            if (result != null) {
                if (result.IsCancelled)
                    log.Warning($"Job run \"{jobName}\" cancelled: {result.Message} Error: {result.Error?.Message}");
                else if (!result.IsSuccess)
                    log.Error($"Job run \"{jobName}\" failed: {result.Message}", result.Error);
                else if (!String.IsNullOrEmpty(result.Message))
                    log.Info($"Job run \"{jobName}\" succeeded: {result.Message}");
                else
                    log.Info($"Job run \"{jobName}\" succeeded.");
            } else {
                log.Error($"Null job run result for \"{jobName}\".");
            }
        }
    }
}
