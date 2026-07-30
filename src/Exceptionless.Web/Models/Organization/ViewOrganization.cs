using Exceptionless.Core.Attributes;
using Exceptionless.Core.Extensions;
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
    public string? IconUrl { get; set; }
    public string PlanId { get; set; } = null!;
    public string PlanName { get; set; } = null!;
    public string PlanDescription { get; set; } = null!;
    public string? CardLast4 { get; set; }
    public DateTime? SubscribeDate { get; set; }
    public DateTime? BillingChangeDate { get; set; }
    [ObjectId]
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
    public ISet<string> Features { get; set; } = new HashSet<string>();
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
        var endDateUtc = timeProvider.GetUtcNow().UtcDateTime.StartOfMonth();
        var startDateUtc = endDateUtc.SubtractMonths(11);
        var organizationCreatedMonthUtc = organization.CreatedUtc.ToUniversalTime().StartOfMonth();
        if (organizationCreatedMonthUtc > startDateUtc)
            startDateUtc = organizationCreatedMonthUtc;

        var knownUsages = organization.Usage
            .Where(u => u.Limit != 0)
            .OrderBy(u => u.Date)
            .ToList();
        int limit = knownUsages
            .LastOrDefault(u => u.Date <= startDateUtc)?.Limit
            ?? knownUsages.FirstOrDefault()?.Limit
            ?? organization.GetMaxEventsPerMonthWithBonus(timeProvider);

        DateTime? bonusExpirationMonthUtc = organization.BonusExpiration?.ToUniversalTime().StartOfMonth();
        int limitAfterBonusExpiration = limit;
        if (bonusExpirationMonthUtc.HasValue)
        {
            int baseLimit = organization.MaxEventsPerMonth <= 0 ? -1 : organization.MaxEventsPerMonth;
            var usageAtBonusExpiration = knownUsages.FirstOrDefault(u =>
                u.Date.Year == bonusExpirationMonthUtc.Value.Year && u.Date.Month == bonusExpirationMonthUtc.Value.Month);
            var usageBeforeBonusExpiration = knownUsages.LastOrDefault(u => u.Date < bonusExpirationMonthUtc.Value);
            DateTime? billingChangeMonthUtc = organization.BillingChangeDate is { } billingChangeDate
                && billingChangeDate > DateTime.MinValue
                    ? billingChangeDate.ToUniversalTime().StartOfMonth()
                    : null;
            bool currentPlanStartedAfterBonusExpiration = billingChangeMonthUtc > bonusExpirationMonthUtc;
            limitAfterBonusExpiration = usageAtBonusExpiration is not null
                ? GetLimitWithoutBonus(usageAtBonusExpiration,
                    currentPlanStartedAfterBonusExpiration && usageBeforeBonusExpiration?.Limit == usageAtBonusExpiration.Limit)
                : usageBeforeBonusExpiration is not null
                    ? GetLimitWithoutBonus(usageBeforeBonusExpiration, currentPlanStartedAfterBonusExpiration)
                    : baseLimit;

            var firstKnownUsage = knownUsages.FirstOrDefault();
            if (startDateUtc < bonusExpirationMonthUtc.Value && firstKnownUsage is not null && firstKnownUsage.Date < bonusExpirationMonthUtc.Value)
                limit = GetLimitWithoutBonus(firstKnownUsage, currentPlanStartedAfterBonusExpiration);
            else if (startDateUtc >= bonusExpirationMonthUtc.Value
                && !knownUsages.Any(u => u.Date >= bonusExpirationMonthUtc.Value && u.Date <= startDateUtc))
                limit = limitAfterBonusExpiration;

            int GetLimitWithoutBonus(UsageInfo knownUsage, bool inferHistoricalBonus)
            {
                bool currentPlanWasActive = billingChangeMonthUtc.HasValue && knownUsage.Date >= billingChangeMonthUtc.Value;
                return knownUsage.Limit > organization.BonusEventsPerMonth
                    && (currentPlanWasActive && knownUsage.Limit == baseLimit + organization.BonusEventsPerMonth || inferHistoricalBonus)
                        ? knownUsage.Limit - organization.BonusEventsPerMonth
                        : knownUsage.Limit;
            }
        }

        while (startDateUtc <= endDateUtc)
        {
            if (startDateUtc == bonusExpirationMonthUtc)
                limit = limitAfterBonusExpiration;

            var usage = organization.Usage.FirstOrDefault(u => u.Date.Year == startDateUtc.Year && u.Date.Month == startDateUtc.Month);
            if (usage is null)
            {
                organization.Usage.Add(new UsageInfo
                {
                    Date = startDateUtc,
                    Limit = limit
                });
            }
            else if (usage.Limit == 0)
            {
                usage.Limit = limit;
            }
            else if (startDateUtc != bonusExpirationMonthUtc)
            {
                limit = usage.Limit;
            }

            startDateUtc = startDateUtc.AddMonths(1).StartOfMonth();
        }
    }

    public static UsageInfo GetCurrentUsage(this ViewOrganization organization, TimeProvider timeProvider)
    {
        return organization.GetUsage(timeProvider.GetUtcNow().UtcDateTime, timeProvider);
    }

    public static UsageInfo GetUsage(this ViewOrganization organization, DateTime date, TimeProvider timeProvider)
    {
        return organization.Usage.GetUsage(date, organization.GetMaxEventsPerMonthWithBonus(timeProvider));
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
