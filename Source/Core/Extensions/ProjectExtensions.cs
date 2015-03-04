using System;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Extensions {
    public static class ProjectExtensions {
        public static void AddDefaultOwnerNotificationSettings(this Project project, string userId, NotificationSettings settings = null) {
            if (project.NotificationSettings.ContainsKey(userId))
                return;

            project.NotificationSettings.Add(userId, settings ?? new NotificationSettings {
                ReportNewErrors = true,
                SendDailySummary = true,
                ReportCriticalErrors = true,
                ReportEventRegressions = true
            });
        }
    }
}