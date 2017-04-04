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
using Foundatio.Metrics;
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
        private static readonly string _metricsPrefix;
        private static readonly IMetricsClient _metricsClient;
        private static readonly ISerializer _serializer;

        static JobRunner() {
            AppDomain.CurrentDomain.SetDataDirectory();
            _loggerFactory = Settings.Current.GetLoggerFactory();
             _serviceProvider = ServiceProvider.GetServiceProvider(Settings.JobBootstrappedServiceProvider, _loggerFactory);
            _metricsClient = _serviceProvider.GetService<IMetricsClient>();
            _serializer = _serviceProvider.GetService<ISerializer>();

            if (!String.IsNullOrEmpty(Settings.Current.AppScope) && !Settings.Current.AppScope.EndsWith("."))
                _metricsPrefix += ".";
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

        public static Task ProcessEventNotificationWorkItemQueueItem(string id, byte[] message, DateTimeOffset insertionTime, string popReceipt, int dequeueCount, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return ProcessQueueItem<EventNotificationsJob, EventNotificationWorkItem>(id, message, insertionTime, popReceipt, dequeueCount, log, token);
        }

        public static Task ProcessEventPostQueueItem(string id, byte[] message, DateTimeOffset insertionTime, string popReceipt, int dequeueCount, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return ProcessQueueItem<EventPostsJob, EventPost>(id, message, insertionTime, popReceipt, dequeueCount, log, token);
        }

        public static Task ProcessEventUserDescriptionQueueItem(string id, byte[] message, DateTimeOffset insertionTime, string popReceipt, int dequeueCount, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return ProcessQueueItem<EventUserDescriptionsJob, EventUserDescription>(id, message, insertionTime, popReceipt, dequeueCount, log, token);
        }

        public static Task ProcessMailMessageQueueItem(string id, byte[] message, DateTimeOffset insertionTime, string popReceipt, int dequeueCount, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return ProcessQueueItem<MailMessageJob, MailMessage>(id, message, insertionTime, popReceipt, dequeueCount, log, token);
        }

        public static Task ProcessWebHookNotificationQueueItem(string id, byte[] message, DateTimeOffset insertionTime, string popReceipt, int dequeueCount, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return ProcessQueueItem<WebHooksJob, WebHookNotification>(id, message, insertionTime, popReceipt, dequeueCount, log, token);
        }

        public static Task ProcessWorkItemDataQueueItem(string id, byte[] message, DateTimeOffset insertionTime, string popReceipt, int dequeueCount, TraceWriter log, CancellationToken token = default(CancellationToken)) {
            return ProcessQueueItem<WorkItemJob, WorkItemData>(id, message, insertionTime, popReceipt, dequeueCount, log, token);
        }

        private static async Task ProcessQueueItem<TJob, TWorkItem>(string id, byte[] message, DateTimeOffset insertionTime, string popReceipt, int dequeueCount, TraceWriter log, CancellationToken token) where TJob : class, IQueueJob<TWorkItem> where TWorkItem : class {
            string jobName = typeof(TWorkItem).Name;
            log.Info($"Processing {jobName} queue item: {id} Attempts: {dequeueCount} Enqueued: {insertionTime:O}");

            var job = _serviceProvider.GetService<TJob>();
            var data = await _serializer.DeserializeAsync<TWorkItem>(message).AnyContext();
            var entry = new AzureStorageQueueEntry<TWorkItem>(new CloudQueueMessage(id, popReceipt), data, job.Queue) {
                Attempts = dequeueCount,
                EnqueuedTimeUtc = insertionTime.UtcDateTime
            };

            log.Info($"Incrementing {jobName} dequeue counter: {id}");
            await IncrementDequeueCountersAsync(entry).AnyContext();

            log.Info($"Running job {jobName}: {id}");
            var result = await job.ProcessAsync(entry, token).AnyContext();
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

        private static async Task IncrementDequeueCountersAsync<T>(IQueueEntry<T> queueEntry) where T : class {
            var metadata = queueEntry as IQueueEntryMetadata;
            string subMetricName = GetSubMetricName(queueEntry.Value);

            if (!String.IsNullOrEmpty(subMetricName))
                await _metricsClient.CounterAsync(GetFullMetricName<T>(subMetricName, "dequeued")).AnyContext();
            await _metricsClient.CounterAsync(GetFullMetricName<T>("dequeued")).AnyContext();

            if (metadata == null || metadata.EnqueuedTimeUtc == DateTime.MinValue || metadata.DequeuedTimeUtc == DateTime.MinValue)
                return;

            var start = metadata.EnqueuedTimeUtc;
            var end = metadata.DequeuedTimeUtc;
            int time = (int)(end - start).TotalMilliseconds;

            if (!String.IsNullOrEmpty(subMetricName))
                await _metricsClient.TimerAsync(GetFullMetricName<T>(subMetricName, "queuetime"), time).AnyContext();
            await _metricsClient.TimerAsync(GetFullMetricName<T>("queuetime"), time).AnyContext();
        }

        private static string GetSubMetricName<T>(T data) where T : class {
            var haveStatName = data as IHaveSubMetricName;
            return haveStatName?.GetSubMetricName();
        }

        private static string GetFullMetricName<T>(string name) where T : class {
            return String.Concat(_metricsPrefix, typeof(T).Name.ToLowerInvariant(), ".", name);
        }

        private static string GetFullMetricName<T>(string customMetricName, string name) where T : class {
            return String.IsNullOrEmpty(customMetricName) ? GetFullMetricName<T>(name) : String.Concat(_metricsPrefix, typeof(T).Name.ToLowerInvariant(), ".", customMetricName.ToLower(), ".", name);
        }
    }
}