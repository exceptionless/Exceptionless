using System;
using System.Collections.Generic;
using System.Linq;
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

        public static string BuildRetentionFilter(this IList<Project> projects, ICollection<Organization> organizations) {
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < projects.Count; index++) {
                if (index > 0)
                    builder.Append(" OR ");

                var project = projects[index];
                var organization = organizations.FirstOrDefault(o => o.Id == project.OrganizationId);
                if (organization != null && organization.RetentionDays > 0)
                    builder.AppendFormat("(project:{0} AND date:[now/d-{1}d TO now/d+1d}})", project.Id, organization.RetentionDays);
                else
                    builder.AppendFormat("project:{0}", project.Id);
            }
            
            return builder.ToString();
        }
    }
}