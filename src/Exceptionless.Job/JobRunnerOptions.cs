using System;
using System.Linq;

namespace Exceptionless.Job {
    public class JobRunnerOptions {
        public JobRunnerOptions(string[] args) {
            if (args.Length > 1)
                throw new ArgumentException("More than one job argument specified. You must either specify 1 named job or don't pass any arguments to run all jobs.");

            CleanupData = args.Length == 0 || args.Contains(nameof(CleanupData), StringComparer.OrdinalIgnoreCase);
            if (CleanupData && args.Length != 0)
                JobName = nameof(CleanupData);

            CleanupOrphanedData = args.Length == 0 || args.Contains(nameof(CleanupOrphanedData), StringComparer.OrdinalIgnoreCase);
            if (CleanupOrphanedData && args.Length != 0)
                JobName = nameof(CleanupOrphanedData);

            CloseInactiveSessions = args.Length == 0 || args.Contains(nameof(CloseInactiveSessions), StringComparer.OrdinalIgnoreCase);
            if (CloseInactiveSessions && args.Length != 0)
                JobName = nameof(CloseInactiveSessions);

            DailySummary = args.Length == 0 || args.Contains(nameof(DailySummary), StringComparer.OrdinalIgnoreCase);
            if (DailySummary && args.Length != 0)
                JobName = nameof(DailySummary);

            DataMigration = args.Contains(nameof(DataMigration), StringComparer.OrdinalIgnoreCase);
            if (DataMigration && args.Length != 0)
                JobName = nameof(DataMigration);

            DownloadGeoIPDatabase = args.Length == 0 || args.Contains(nameof(DownloadGeoIPDatabase), StringComparer.OrdinalIgnoreCase);
            if (DownloadGeoIPDatabase && args.Length != 0)
                JobName = nameof(DownloadGeoIPDatabase);

            EventNotifications = args.Length == 0 || args.Contains(nameof(EventNotifications), StringComparer.OrdinalIgnoreCase);
            if (EventNotifications && args.Length != 0)
                JobName = nameof(EventNotifications);

            EventPosts = args.Length == 0 || args.Contains(nameof(EventPosts), StringComparer.OrdinalIgnoreCase);
            if (EventPosts && args.Length != 0)
                JobName = nameof(EventPosts);

            EventUserDescriptions = args.Length == 0 || args.Contains(nameof(EventUserDescriptions), StringComparer.OrdinalIgnoreCase);
            if (EventUserDescriptions && args.Length != 0)
                JobName = nameof(EventUserDescriptions);

            MailMessage = args.Length == 0 || args.Contains(nameof(MailMessage), StringComparer.OrdinalIgnoreCase);
            if (MailMessage && args.Length != 0)
                JobName = nameof(MailMessage);

            MaintainIndexes = args.Length == 0 || args.Contains(nameof(MaintainIndexes), StringComparer.OrdinalIgnoreCase);
            if (MaintainIndexes && args.Length != 0)
                JobName = nameof(MaintainIndexes);

            Migration = args.Length == 0 || args.Contains(nameof(Migration), StringComparer.OrdinalIgnoreCase);
            if (Migration && args.Length != 0)
                JobName = nameof(Migration);

            StackStatus = args.Length == 0 || args.Contains(nameof(StackStatus), StringComparer.OrdinalIgnoreCase);
            if (StackStatus && args.Length != 0)
                JobName = nameof(StackStatus);

            StackEventCount = args.Length == 0 || args.Contains(nameof(StackEventCount), StringComparer.OrdinalIgnoreCase);
            if (StackEventCount && args.Length != 0)
                JobName = nameof(StackEventCount);

            WebHooks = args.Length == 0 || args.Contains(nameof(WebHooks), StringComparer.OrdinalIgnoreCase);
            if (WebHooks && args.Length != 0)
                JobName = nameof(WebHooks);

            WorkItem = args.Length == 0 || args.Contains(nameof(WorkItem), StringComparer.OrdinalIgnoreCase);
            if (WorkItem && args.Length != 0)
                JobName = nameof(WorkItem);
        }

        public string JobName { get; }
        public bool CleanupData { get; }
        public bool CleanupOrphanedData { get; }
        public bool CloseInactiveSessions { get; }
        public bool DailySummary { get; }
        public bool DataMigration { get; }
        public bool DownloadGeoIPDatabase { get; }
        public bool EventNotifications { get; }
        public bool EventPosts { get; }
        public bool EventUserDescriptions { get; }
        public bool MailMessage { get; }
        public bool MaintainIndexes { get; }
        public bool Migration { get; }
        public bool StackStatus { get; }
        public bool StackEventCount { get; }
        public bool WebHooks { get; }
        public bool WorkItem { get; }
    }
}
