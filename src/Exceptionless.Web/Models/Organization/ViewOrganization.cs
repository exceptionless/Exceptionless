using Exceptionless.Core.Attributes;
using Exceptionless.Core.Models;
using Exceptionless.DateTimeExtensions;
using Foundatio.Repositories.Models;

namespace Exceptionless.Web.Models;

public record ViewOrganization : IIdentity, IData, IHaveDates
{
    [ObjectId]
    public string Id { get; set; } = null!;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string Name { get; set; } = null!;
    public string PlanId { get; set; } = null!;
    public string PlanName { get; set; } = null!;
    public string PlanDescription { get; set; } = null!;
    public string? CardLast4 { get; set; }
    public DateTime? SubscribeDate { get; set; }
    public DateTime? BillingChangeDate { get; set; }
    public string? BillingChangedByUserId { get; set; }
    public BillingStatus BillingStatus { get; set; }
    public decimal BillingPrice { get; set; }
    public int MaxEventsPerMonth { get; set; }
    public int BonusEventsPerMonth { get; set; }
    public DateTime? BonusExpiration { get; set; }
    public int RetentionDays { get; set; }
    public bool IsSuspended { get; set; }
    public string? SuspensionCode { get; set; }
    public string? SuspensionNotes { get; set; }
    public DateTime? SuspensionDate { get; set; }
    public bool HasPremiumFeatures { get; set; }
    public int MaxUsers { get; set; }
    public int MaxProjects { get; set; }
    public long ProjectCount { get; set; }
    public long StackCount { get; set; }
    public long EventCount { get; set; }
    public ICollection<Invite> Invites { get; set; } = null!;
    public ICollection<UsageHourInfo> UsageHours { get; set; } = new SortedSet<UsageHourInfo>(Comparer<UsageHourInfo>.Create((a, b) => a.Date.CompareTo(b.Date)));
    public ICollection<UsageInfo> Usage { get; set; } = new SortedSet<UsageInfo>(Comparer<UsageInfo>.Create((a, b) => a.Date.CompareTo(b.Date)));
    public Core.Models.DataDictionary? Data { get; set; }

    public bool IsThrottled { get; set; }
    public bool IsOverMonthlyLimit { get; set; }
    public bool IsOverRequestLimit { get; set; }
}

public static class ViewOrganizationExtensions
{
    public static UsageHourInfo GetHourlyUsage(this ViewOrganization organization, DateTime date)
    {
        var startOfHour = date.ToUniversalTime().StartOfMonth();
        var overage = organization.UsageHours.FirstOrDefault(o => o.Date.Equals(startOfHour));
        if (overage is not null)
            return overage;

        overage = new UsageHourInfo
        {
            Date = startOfHour
        };
        organization.UsageHours.Add(overage);

        return overage;
    }

    public static UsageHourInfo GetCurrentHourlyUsage(this ViewOrganization organization, TimeProvider timeProvider)
    {
        return organization.GetHourlyUsage(timeProvider.GetUtcNow().UtcDateTime);
    }

    public static void EnsureUsage(this ViewOrganization organization, TimeProvider timeProvider)
    {
        var startDate = timeProvider.GetUtcNow().UtcDateTime.SubtractMonths(11).StartOfMonth();

        while (startDate <= timeProvider.GetUtcNow().UtcDateTime.StartOfMonth())
        {
            organization.GetUsage(startDate, timeProvider);
            startDate = startDate.AddMonths(1).StartOfMonth();
        }
    }

    public static UsageInfo GetCurrentUsage(this ViewOrganization organization, TimeProvider timeProvider)
    {
        return organization.GetUsage(timeProvider.GetUtcNow().UtcDateTime, timeProvider);
    }

    public static UsageInfo GetUsage(this ViewOrganization organization, DateTime date, TimeProvider timeProvider)
    {
        var startOfMonth = date.ToUniversalTime().StartOfMonth();
        var usage = organization.Usage.FirstOrDefault(o => o.Date.Year == startOfMonth.Year && o.Date.Month == startOfMonth.Month);
        if (usage is not null)
            return usage;

        usage = new UsageInfo
        {
            Date = startOfMonth,
            Limit = organization.GetMaxEventsPerMonthWithBonus(timeProvider)
        };
        organization.Usage.Add(usage);

        return usage;
    }

    public static int GetMaxEventsPerMonthWithBonus(this ViewOrganization organization, TimeProvider timeProvider)
    {
        if (organization.MaxEventsPerMonth <= 0)
            return -1;

        int bonusEvents = organization.BonusExpiration.HasValue && organization.BonusExpiration > timeProvider.GetUtcNow().UtcDateTime ? organization.BonusEventsPerMonth : 0;
        return organization.MaxEventsPerMonth + bonusEvents;
    }

    public static void TrimUsage(this ViewOrganization organization, TimeProvider timeProvider)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        // keep 1 year of usage
        organization.Usage = organization.Usage.Except(organization.Usage
            .Where(u => utcNow.Subtract(u.Date) > TimeSpan.FromDays(366)))
            .OrderBy(u => u.Date)
            .ToList();

        // keep 30 days of hourly usage that have blocked events, otherwise keep it for 7 days
        organization.UsageHours = organization.UsageHours.Except(organization.UsageHours
            .Where(u => utcNow.Subtract(u.Date) > TimeSpan.FromDays(u.Blocked > 0 ? 30 : 7)))
            .OrderBy(u => u.Date)
            .ToList();
    }
}
