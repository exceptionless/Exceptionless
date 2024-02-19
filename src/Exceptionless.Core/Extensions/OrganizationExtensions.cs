using Exceptionless.Core.Models;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Utility;

namespace Exceptionless.Core.Extensions;

public static class OrganizationExtensions
{
    public static Invite? GetInvite(this Organization organization, string token)
    {
        if (String.IsNullOrEmpty(token))
            return null;

        return organization.Invites.FirstOrDefault(i => String.Equals(i.Token, token, StringComparison.OrdinalIgnoreCase));
    }

    public static DateTime GetRetentionUtcCutoff(this Organization organization, Project project, int maximumRetentionDays)
    {
        return organization.GetRetentionUtcCutoff(maximumRetentionDays, project.CreatedUtc.SafeSubtract(TimeSpan.FromDays(3)));
    }

    public static DateTime GetRetentionUtcCutoff(this Organization organization, Stack stack, int maximumRetentionDays)
    {
        return organization.GetRetentionUtcCutoff(maximumRetentionDays, stack.FirstOccurrence);
    }

    public static DateTime GetRetentionUtcCutoff(this Organization organization, int maximumRetentionDays, DateTime? oldestPossibleEventAge = null)
    {
        // NOTE: We allow you to submit events 3 days before your creation date.
        var oldestPossibleOrganizationEventAge = organization.CreatedUtc.Date.SafeSubtract(TimeSpan.FromDays(3));
        if (!oldestPossibleEventAge.HasValue || oldestPossibleEventAge.Value.IsBefore(oldestPossibleOrganizationEventAge))
            oldestPossibleEventAge = oldestPossibleOrganizationEventAge;

        int retentionDays = organization.RetentionDays > 0 ? organization.RetentionDays : maximumRetentionDays;
        var retentionDate = retentionDays <= 0 ? oldestPossibleEventAge.Value : SystemClock.UtcNow.Date.AddDays(-retentionDays);
        return retentionDate.IsAfter(oldestPossibleEventAge.Value) ? retentionDate : oldestPossibleEventAge.Value;
    }

    public static DateTime GetRetentionUtcCutoff(this IReadOnlyCollection<Organization> organizations, int maximumRetentionDays)
    {
        return organizations.Count > 0 ? organizations.Min(o => o.GetRetentionUtcCutoff(maximumRetentionDays)) : DateTime.MinValue;
    }

    public static void RemoveSuspension(this Organization organization)
    {
        organization.IsSuspended = false;
        organization.SuspensionDate = null;
        organization.SuspensionCode = null;
        organization.SuspensionNotes = null;
        organization.SuspendedByUserId = null;
    }

    public static async Task<bool> IsOverRequestLimitAsync(string organizationId, ICacheClient cacheClient, int apiThrottleLimit)
    {
        if (apiThrottleLimit == Int32.MaxValue)
            return false;

        string cacheKey = String.Concat("api", ":", organizationId, ":", SystemClock.UtcNow.Floor(TimeSpan.FromMinutes(15)).Ticks);
        var limit = await cacheClient.GetAsync<long>(cacheKey);
        return limit.HasValue && limit.Value >= apiThrottleLimit;
    }

    public static int GetMaxEventsPerMonthWithBonus(this Organization organization)
    {
        if (organization.MaxEventsPerMonth <= 0)
            return -1;

        int bonusEvents = organization.BonusExpiration.HasValue && organization.BonusExpiration > SystemClock.UtcNow ? organization.BonusEventsPerMonth : 0;
        return organization.MaxEventsPerMonth + bonusEvents;
    }

    public static bool IsOverMonthlyLimit(this Organization organization)
    {
        if (organization.MaxEventsPerMonth < 0)
            return false;

        return organization.GetCurrentUsage().Total >= organization.GetMaxEventsPerMonthWithBonus();
    }

    public static bool HasHourlyUsage(this Organization organization, DateTime date)
    {
        return organization.UsageHours.Any(o => o.Date == date.ToUniversalTime().StartOfHour());
    }

    public static UsageHourInfo GetHourlyUsage(this Organization organization, DateTime date)
    {
        var overage = organization.UsageHours.FirstOrDefault(o => o.Date == date.ToUniversalTime().StartOfHour());
        if (overage is not null)
            return overage;

        overage = new UsageHourInfo
        {
            Date = date.ToUniversalTime().StartOfHour()
        };
        organization.UsageHours.Add(overage);

        return overage;
    }

    public static UsageHourInfo GetCurrentHourlyUsage(this Organization organization)
    {
        return organization.GetHourlyUsage(SystemClock.UtcNow);
    }

    public static void TrimUsage(this Organization organization)
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

    public static UsageInfo GetCurrentUsage(this Organization organization)
    {
        return organization.GetUsage(SystemClock.UtcNow);
    }

    public static UsageInfo GetUsage(this Organization organization, DateTime date)
    {
        var startOfMonth = date.ToUniversalTime().StartOfMonth();
        var usage = organization.Usage.FirstOrDefault(o => o.Date.Year == startOfMonth.Year && o.Date.Month == startOfMonth.Month);
        if (usage is not null)
            return usage;

        usage = new UsageInfo
        {
            Date = startOfMonth,
            Limit = organization.GetMaxEventsPerMonthWithBonus()
        };
        organization.Usage.Add(usage);

        return usage;
    }
}
