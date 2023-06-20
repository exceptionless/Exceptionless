﻿using Exceptionless.Core.Models;
using Exceptionless.DateTimeExtensions;
using Foundatio.Repositories.Models;
using Foundatio.Utility;

namespace Exceptionless.Web.Models;

public class ViewOrganization : IIdentity, IData, IHaveCreatedDate
{
    public string Id { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string Name { get; set; }
    public string PlanId { get; set; }
    public string PlanName { get; set; }
    public string PlanDescription { get; set; }
    public string CardLast4 { get; set; }
    public DateTime? SubscribeDate { get; set; }
    public DateTime? BillingChangeDate { get; set; }
    public string BillingChangedByUserId { get; set; }
    public BillingStatus BillingStatus { get; set; }
    public decimal BillingPrice { get; set; }
    public int MaxEventsPerMonth { get; set; }
    public int BonusEventsPerMonth { get; set; }
    public DateTime? BonusExpiration { get; set; }
    public int RetentionDays { get; set; }
    public bool IsSuspended { get; set; }
    public string SuspensionCode { get; set; }
    public string SuspensionNotes { get; set; }
    public DateTime? SuspensionDate { get; set; }
    public bool HasPremiumFeatures { get; set; }
    public int MaxUsers { get; set; }
    public int MaxProjects { get; set; }
    public long ProjectCount { get; set; }
    public long StackCount { get; set; }
    public long EventCount { get; set; }
    public ICollection<Invite> Invites { get; set; }
    public ICollection<UsageHourInfo> UsageHours { get; set; } = new SortedSet<UsageHourInfo>(Comparer<UsageHourInfo>.Create((a, b) => a.Date.CompareTo(b.Date)));
    public ICollection<UsageInfo> Usage { get; set; } = new SortedSet<UsageInfo>(Comparer<UsageInfo>.Create((a, b) => a.Date.CompareTo(b.Date)));
    public Core.Models.DataDictionary Data { get; set; }

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
        if (overage != null)
            return overage;

        overage = new UsageHourInfo
        {
            Date = startOfHour
        };
        organization.UsageHours.Add(overage);

        return overage;
    }

    public static UsageHourInfo GetCurrentHourlyUsage(this ViewOrganization organization)
    {
        return organization.GetHourlyUsage(SystemClock.UtcNow);
    }

    public static void EnsureUsage(this ViewOrganization organization)
    {
        var startDate = SystemClock.UtcNow.SubtractMonths(11).StartOfMonth();

        while (startDate <= SystemClock.UtcNow.StartOfMonth())
        {
            organization.GetUsage(startDate);
            startDate = startDate.AddMonths(1).StartOfMonth();
        }
    }

    public static UsageInfo GetCurrentUsage(this ViewOrganization organization)
    {
        return organization.GetUsage(SystemClock.UtcNow);
    }

    public static UsageInfo GetUsage(this ViewOrganization organization, DateTime date)
    {
        var startOfMonth = date.ToUniversalTime().StartOfMonth();
        var usage = organization.Usage.FirstOrDefault(o => o.Date.Year == startOfMonth.Year && o.Date.Month == startOfMonth.Month);
        if (usage != null)
            return usage;

        usage = new UsageInfo
        {
            Date = startOfMonth,
            Limit = organization.GetMaxEventsPerMonthWithBonus()
        };
        organization.Usage.Add(usage);

        return usage;
    }

    public static int GetMaxEventsPerMonthWithBonus(this ViewOrganization organization)
    {
        if (organization.MaxEventsPerMonth <= 0)
            return -1;

        int bonusEvents = organization.BonusExpiration.HasValue && organization.BonusExpiration > SystemClock.UtcNow ? organization.BonusEventsPerMonth : 0;
        return organization.MaxEventsPerMonth + bonusEvents;
    }

    public static void TrimUsage(this ViewOrganization organization)
    {
        // keep 1 year of usage
        organization.Usage = organization.Usage.Except(organization.Usage
            .Where(u => SystemClock.UtcNow.Subtract(u.Date) > TimeSpan.FromDays(366)))
            .ToList();

        // keep 30 days of hourly usage that have blocked events, otherwise keep it for 7 days
        organization.UsageHours = organization.UsageHours.Except(organization.UsageHours
            .Where(u => SystemClock.UtcNow.Subtract(u.Date) > TimeSpan.FromDays(u.Blocked > 0 ? 30 : 7)))
            .ToList();
    }
}
