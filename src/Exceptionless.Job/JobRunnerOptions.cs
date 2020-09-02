using System;
using System.Linq;

namespace Exceptionless.Job {
    public class JobRunnerOptions {
        public JobRunnerOptions(string[] args) {
            if (args.Length > 1)
                throw new ArgumentException("More than one job argument specified. You must either specify 1 named job or don't pass any arguments to run all jobs.");

            CleanupData = args.Length == 0 || args.Contains("CleanupData", StringComparer.OrdinalIgnoreCase);
            if (EventPosts && args.Length != 0)
                JobName = "CleanupData";

            CloseInactiveSessions = args.Length == 0 || args.Contains("CloseInactiveSessions", StringComparer.OrdinalIgnoreCase);
            if (CloseInactiveSessions && args.Length != 0)
                JobName = "CloseInactiveSessions";

            DailySummary = args.Length == 0 || args.Contains("DailySummary", StringComparer.OrdinalIgnoreCase);
            if (DailySummary && args.Length != 0)
                JobName = "DailySummary";

            DataMigration = args.Contains("DataMigration", StringComparer.OrdinalIgnoreCase);
            if (DataMigration && args.Length != 0)
                JobName = "DataMigration";

            DownloadGeoipDatabase = args.Length == 0 || args.Contains("DownloadGeoIPDatabase", StringComparer.OrdinalIgnoreCase);
            if (DownloadGeoipDatabase && args.Length != 0)
                JobName = "DownloadGeoIPDatabase";

            EventNotifications = args.Length == 0 || args.Contains("EventNotifications", StringComparer.OrdinalIgnoreCase);
            if (EventNotifications && args.Length != 0)
                JobName = "EventNotifications";

            EventPosts = args.Length == 0 || args.Contains("EventPosts", StringComparer.OrdinalIgnoreCase);
            if (EventPosts && args.Length != 0)
                JobName = "EventPosts";

            EventUserDescriptions = args.Length == 0 || args.Contains("EventUserDescriptions", StringComparer.OrdinalIgnoreCase);
            if (EventUserDescriptions && args.Length != 0)
                JobName = "EventUserDescriptions";

            MailMessage = args.Length == 0 || args.Contains("MailMessage", StringComparer.OrdinalIgnoreCase);
            if (MailMessage && args.Length != 0)
                JobName = "MailMessage";

            MaintainIndexes = args.Length == 0 || args.Contains("MaintainIndexes", StringComparer.OrdinalIgnoreCase);
            if (MaintainIndexes && args.Length != 0)
                JobName = "MaintainIndexes";

            Migration = args.Length == 0 || args.Contains("Migration", StringComparer.OrdinalIgnoreCase);
            if (Migration && args.Length != 0)
                JobName = "Migration";

            StackStatus = args.Length == 0 || args.Contains("StackStatus", StringComparer.OrdinalIgnoreCase);
            if (StackStatus && args.Length != 0)
                JobName = "StackStatus";

            StackEventCount = args.Length == 0 || args.Contains("StackEventCount", StringComparer.OrdinalIgnoreCase);
            if (StackEventCount && args.Length != 0)
                JobName = "StackEventCount";

            WebHooks = args.Length == 0 || args.Contains("WebHooks", StringComparer.OrdinalIgnoreCase);
            if (WebHooks && args.Length != 0)
                JobName = "WebHooks";

            WorkItem = args.Length == 0 || args.Contains("WorkItem", StringComparer.OrdinalIgnoreCase);
            if (WorkItem && args.Length != 0)
                JobName = "WorkItem";
        }

        public string JobName { get; }
        public bool CleanupData { get; }
        public bool CloseInactiveSessions { get; }
        public bool DailySummary { get; }
        public bool DataMigration { get; }
        public bool DownloadGeoipDatabase { get; }
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
