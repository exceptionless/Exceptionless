using Exceptionless.Core.Models;
using Exceptionless.DateTimeExtensions;
using Foundatio.Repositories.Models;
using Foundatio.Utility;

namespace Exceptionless.Web.Models;

public class ViewProject : IIdentity, IData, IHaveCreatedDate {
    public string Id { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string OrganizationId { get; set; }
    public string OrganizationName { get; set; }
    public string Name { get; set; }
    public bool DeleteBotDataEnabled { get; set; }
    public Core.Models.DataDictionary Data { get; set; }
    public HashSet<string> PromotedTabs { get; set; }
    public bool? IsConfigured { get; set; }
    public long StackCount { get; set; }
    public long EventCount { get; set; }
    public bool HasPremiumFeatures { get; set; }
    public bool HasSlackIntegration { get; set; }
    public ICollection<UsageHourInfo> UsageHours { get; set; } = new SortedSet<UsageHourInfo>(Comparer<UsageHourInfo>.Create((a, b) => a.Date.CompareTo(b.Date)));
    public ICollection<UsageInfo> Usage { get; set; } = new SortedSet<UsageInfo>(Comparer<UsageInfo>.Create((a, b) => a.Date.CompareTo(b.Date)));
}

public static class ViewProjectExtensions {
    public static UsageHourInfo GetHourlyUsage(this ViewProject project, DateTime date) {
        var overage = project.UsageHours.FirstOrDefault(o => o.Date == date.ToUniversalTime().StartOfHour());
        if (overage != null)
            return overage;

        overage = new UsageHourInfo {
            Date = date.ToUniversalTime().StartOfHour()
        };
        project.UsageHours.Add(overage);

        return overage;
    }

    public static UsageHourInfo GetCurrentHourlyUsage(this ViewProject project) {
        return project.GetHourlyUsage(SystemClock.UtcNow);
    }

    public static void EnsureUsage(this ViewProject project) {
        var startDate = SystemClock.UtcNow.SubtractYears(1).StartOfMonth();

        while (startDate < SystemClock.UtcNow.StartOfMonth()) {
            project.GetUsage(startDate);
            startDate = startDate.AddMonths(1).StartOfMonth();
        }
    }

    public static UsageInfo GetCurrentUsage(this ViewProject project) {
        return project.GetUsage(SystemClock.UtcNow);
    }

    public static UsageInfo GetUsage(this ViewProject project, DateTime date) {
        var usage = project.Usage.FirstOrDefault(o => o.Date == date.ToUniversalTime().StartOfMonth());
        if (usage != null)
            return usage;

        usage = new UsageInfo {
            Date = date.ToUniversalTime().StartOfMonth(),
        };
        project.Usage.Add(usage);

        return usage;
    }
}
