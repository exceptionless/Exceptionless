using System.Text;
using Exceptionless.Core.Models;
using Exceptionless.DateTimeExtensions;
using Foundatio.Utility;

namespace Exceptionless.Core.Extensions;

public static class ProjectExtensions {
    /// <summary>
    /// These are the default settings for the integration or user who created the project.
    /// </summary>
    public static void AddDefaultNotificationSettings(this Project project, string userIdOrIntegration, NotificationSettings settings = null) {
        if (project.NotificationSettings.ContainsKey(userIdOrIntegration))
            return;

        project.NotificationSettings.Add(userIdOrIntegration, settings ?? new NotificationSettings {
            SendDailySummary = true,
            ReportNewErrors = true,
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
        var builder = new StringBuilder();
        for (int index = 0; index < projects.Count; index++) {
            if (index > 0)
                builder.Append(" OR ");

            builder.AppendFormat("project:{0}", projects[index].Id);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Gets the slack token from extended data.
    /// </summary>
    public static SlackToken GetSlackToken(this Project project) {
        return project.Data.TryGetValue(Project.KnownDataKeys.SlackToken, out object value) ? value as SlackToken : null;
    }

    public static bool HasOverage(this Project project, DateTime date) {
        return project.OverageHours.Any(o => o.Date == date.StartOfHour());
    }

    public static UsageInfo GetOverage(this Project project, DateTime date) {
        var usage = project.OverageHours.FirstOrDefault(o => o.Date == date.StartOfHour());
        if (usage != null)
            return usage;

        usage = new UsageInfo {
            Date = date.StartOfHour()
        };
        project.OverageHours.Add(usage);

        return usage;
    }

    public static UsageInfo GetCurrentUsage(this Project project) {
        return project.GetUsage(SystemClock.UtcNow);
    }

    public static UsageInfo GetUsage(this Project project, DateTime date) {
        var usage = project.Usage.FirstOrDefault(o => o.Date == date.StartOfMonth());
        if (usage != null)
            return usage;

        usage = new UsageInfo {
            Date = date.StartOfMonth(),
        };
        project.Usage.Add(usage);

        return usage;
    }
}
