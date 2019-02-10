using System;
using System.Linq;
using System.Text;

namespace Exceptionless.Job {
    public class JobRunnerOptions {
        public JobRunnerOptions(string[] args) {
            var description = new StringBuilder();

            CleanupSnapshot = args.Length == 0 || args.Contains("CleanupSnapshot", StringComparer.OrdinalIgnoreCase);
            if (CleanupSnapshot)
                description.Append("CleanupSnapshot,");

            CloseInactiveSessions = args.Length == 0 || args.Contains("CloseInactiveSessions", StringComparer.OrdinalIgnoreCase);
            if (CloseInactiveSessions)
                description.Append("CloseInactiveSessions,");

            DailySummary = args.Length == 0 || args.Contains("DailySummary", StringComparer.OrdinalIgnoreCase);
            if (DailySummary)
                description.Append("DailySummary,");

            DownloadGeoipDatabase = args.Length == 0 || args.Contains("DownloadGeoipDatabase", StringComparer.OrdinalIgnoreCase);
            if (DownloadGeoipDatabase)
                description.Append("DownloadGeoipDatabase,");

            EventNotifications = args.Length == 0 || args.Contains("EventNotifications", StringComparer.OrdinalIgnoreCase);
            if (EventNotifications)
                description.Append("EventNotifications,");

            EventPosts = args.Length == 0 || args.Contains("EventPosts", StringComparer.OrdinalIgnoreCase);
            if (EventPosts)
                description.Append("EventPosts,");

            EventSnapshot = args.Length == 0 || args.Contains("EventSnapshot", StringComparer.OrdinalIgnoreCase);
            if (EventSnapshot)
                description.Append("EventSnapshot,");

            EventUserDescriptions = args.Length == 0 || args.Contains("EventUserDescriptions", StringComparer.OrdinalIgnoreCase);
            if (EventUserDescriptions)
                description.Append("EventUserDescriptions,");

            MailMessage = args.Length == 0 || args.Contains("MailMessage", StringComparer.OrdinalIgnoreCase);
            if (MailMessage)
                description.Append("MailMessage,");

            MaintainIndexes = args.Length == 0 || args.Contains("MaintainIndexes", StringComparer.OrdinalIgnoreCase);
            if (MaintainIndexes)
                description.Append("MaintainIndexes,");

            OrganizationSnapshot = args.Length == 0 || args.Contains("OrganizationSnapshot", StringComparer.OrdinalIgnoreCase);
            if (OrganizationSnapshot)
                description.Append("OrganizationSnapshot,");

            RetentionLimits = args.Length == 0 || args.Contains("RetentionLimits", StringComparer.OrdinalIgnoreCase);
            if (RetentionLimits)
                description.Append("RetentionLimits,");

            StackEventCount = args.Length == 0 || args.Contains("StackEventCount", StringComparer.OrdinalIgnoreCase);
            if (StackEventCount)
                description.Append("StackEventCount,");

            StackSnapshot = args.Length == 0 || args.Contains("StackSnapshot", StringComparer.OrdinalIgnoreCase);
            if (StackSnapshot)
                description.Append("StackSnapshot,");

            WebHooks = args.Length == 0 || args.Contains("WebHooks", StringComparer.OrdinalIgnoreCase);
            if (WebHooks)
                description.Append("WebHooks,");

            WorkItem = args.Length == 0 || args.Contains("WorkItem", StringComparer.OrdinalIgnoreCase);
            if (WorkItem)
                description.Append("WorkItem,");

            if (description.Length == 0)
                Description = "(None)";
            else
                Description = description.ToString(0, description.Length - 1);
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
