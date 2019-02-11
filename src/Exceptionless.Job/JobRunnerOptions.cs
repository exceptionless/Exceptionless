using System;
using System.Linq;

namespace Exceptionless.Job {
    public class JobRunnerOptions {
        public JobRunnerOptions(string[] args) {
            if (args.Length > 1)
                throw new ArgumentException("More than one job argument specified. You must either specify 1 named job or don't pass any arguments to run all jobs.");

            CleanupSnapshot = args.Length == 0 || args.Contains("CleanupSnapshot", StringComparer.OrdinalIgnoreCase);
            if (CleanupSnapshot)
                Description = "CleanupSnapshot";

            CloseInactiveSessions = args.Length == 0 || args.Contains("CloseInactiveSessions", StringComparer.OrdinalIgnoreCase);
            if (CloseInactiveSessions)
                Description = "CloseInactiveSessions";

            DailySummary = args.Length == 0 || args.Contains("DailySummary", StringComparer.OrdinalIgnoreCase);
            if (DailySummary)
                Description = "DailySummary";

            DownloadGeoipDatabase = args.Length == 0 || args.Contains("DownloadGeoipDatabase", StringComparer.OrdinalIgnoreCase);
            if (DownloadGeoipDatabase)
                Description = "DownloadGeoipDatabase";

            EventNotifications = args.Length == 0 || args.Contains("EventNotifications", StringComparer.OrdinalIgnoreCase);
            if (EventNotifications)
                Description = "EventNotifications";

            EventPosts = args.Length == 0 || args.Contains("EventPosts", StringComparer.OrdinalIgnoreCase);
            if (EventPosts)
                Description = "EventPosts";

            EventSnapshot = args.Length == 0 || args.Contains("EventSnapshot", StringComparer.OrdinalIgnoreCase);
            if (EventSnapshot)
                Description = "EventSnapshot";

            EventUserDescriptions = args.Length == 0 || args.Contains("EventUserDescriptions", StringComparer.OrdinalIgnoreCase);
            if (EventUserDescriptions)
                Description = "EventUserDescriptions";

            MailMessage = args.Length == 0 || args.Contains("MailMessage", StringComparer.OrdinalIgnoreCase);
            if (MailMessage)
                Description = "MailMessage";

            MaintainIndexes = args.Length == 0 || args.Contains("MaintainIndexes", StringComparer.OrdinalIgnoreCase);
            if (MaintainIndexes)
                Description = "MaintainIndexes";

            OrganizationSnapshot = args.Length == 0 || args.Contains("OrganizationSnapshot", StringComparer.OrdinalIgnoreCase);
            if (OrganizationSnapshot)
                Description = "OrganizationSnapshot";

            RetentionLimits = args.Length == 0 || args.Contains("RetentionLimits", StringComparer.OrdinalIgnoreCase);
            if (RetentionLimits)
                Description = "RetentionLimits";

            StackEventCount = args.Length == 0 || args.Contains("StackEventCount", StringComparer.OrdinalIgnoreCase);
            if (StackEventCount)
                Description = "StackEventCount";

            StackSnapshot = args.Length == 0 || args.Contains("StackSnapshot", StringComparer.OrdinalIgnoreCase);
            if (StackSnapshot)
                Description = "StackSnapshot";

            WebHooks = args.Length == 0 || args.Contains("WebHooks", StringComparer.OrdinalIgnoreCase);
            if (WebHooks)
                Description = "WebHooks";

            WorkItem = args.Length == 0 || args.Contains("WorkItem", StringComparer.OrdinalIgnoreCase);
            if (WorkItem)
                Description = "WorkItem";

            if (args.Length == 0)
                Description = "All";
        }

        public string Description { get; }

        public bool CleanupSnapshot { get; }
        public bool CloseInactiveSessions { get; }
        public bool DailySummary { get; }
        public bool DownloadGeoipDatabase { get; }
        public bool EventNotifications { get; }
        public bool EventPosts { get; }
        public bool EventSnapshot { get; }
        public bool EventUserDescriptions { get; }
        public bool MailMessage { get; }
        public bool MaintainIndexes { get; }
        public bool OrganizationSnapshot { get; }
        public bool RetentionLimits { get; }
        public bool StackEventCount { get; }
        public bool StackSnapshot { get; }
        public bool WebHooks { get; }
        public bool WorkItem { get; }
    }
}
