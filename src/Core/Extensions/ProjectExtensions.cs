using System;
using System.Collections.Generic;
using System.Text;
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

        public static void SetDefaultUserAgentBotPatterns(this Project project) {
            if (project.Configuration.Settings.ContainsKey(SettingsDictionary.KnownKeys.UserAgentBotPatterns))
                return;
            
            project.Configuration.Settings[SettingsDictionary.KnownKeys.UserAgentBotPatterns] = "*bot*,*crawler*,*spider*,*aolbuild*,*teoma*,*yahoo*";
        }

        public static string BuildFilter(this IList<Project> projects) {
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < projects.Count; index++) {
                if (index > 0)
                    builder.Append(" OR ");
                
                builder.AppendFormat("project:{0}", projects[index].Id);
            }

            return builder.ToString();
        }
    }
}