using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models;
using Foundatio.Caching;
using Foundatio.Utility;

namespace Exceptionless.Core.Extensions;

public static class OrganizationExtensions {
    public static Invite GetInvite(this Organization organization, string token) {
        if (organization == null || String.IsNullOrEmpty(token))
            return null;

        return organization.Invites.FirstOrDefault(i => String.Equals(i.Token, token, StringComparison.OrdinalIgnoreCase));
    }

    public static DateTime GetRetentionUtcCutoff(this Organization organization, Project project, int maximumRetentionDays) {
        return organization.GetRetentionUtcCutoff(maximumRetentionDays, project.CreatedUtc.SafeSubtract(TimeSpan.FromDays(3)));
    }

    public static DateTime GetRetentionUtcCutoff(this Organization organization, Stack stack, int maximumRetentionDays) {
        return organization.GetRetentionUtcCutoff(maximumRetentionDays, stack.FirstOccurrence);
    }

    public static DateTime GetRetentionUtcCutoff(this Organization organization, int maximumRetentionDays, DateTime? oldestPossibleEventAge = null) {
        // NOTE: We allow you to submit events 3 days before your creation date.
        var oldestPossibleOrganizationEventAge = organization.CreatedUtc.Date.SafeSubtract(TimeSpan.FromDays(3));
        if (!oldestPossibleEventAge.HasValue || oldestPossibleEventAge.Value.IsBefore(oldestPossibleOrganizationEventAge))
            oldestPossibleEventAge = oldestPossibleOrganizationEventAge;

        int retentionDays = organization.RetentionDays > 0 ? organization.RetentionDays : maximumRetentionDays;
        var retentionDate = retentionDays <= 0 ? oldestPossibleEventAge.Value : SystemClock.UtcNow.Date.AddDays(-retentionDays);
        return retentionDate.IsAfter(oldestPossibleEventAge.Value) ? retentionDate : oldestPossibleEventAge.Value;
    }

    public static DateTime GetRetentionUtcCutoff(this IReadOnlyCollection<Organization> organizations, int maximumRetentionDays) {
        return organizations.Count > 0 ? organizations.Min(o => o.GetRetentionUtcCutoff(maximumRetentionDays)) : DateTime.MinValue;
    }

    public static void RemoveSuspension(this Organization organization) {
        organization.IsSuspended = false;
        organization.SuspensionDate = null;
        organization.SuspensionCode = null;
        organization.SuspensionNotes = null;
        organization.SuspendedByUserId = null;
    }

    public static async Task<bool> IsOverRequestLimitAsync(string organizationId, ICacheClient cacheClient, int apiThrottleLimit) {
        if (apiThrottleLimit == Int32.MaxValue)
            return false;

        string cacheKey = String.Concat("api", ":", organizationId, ":", SystemClock.UtcNow.Floor(TimeSpan.FromMinutes(15)).Ticks);
        var limit = await cacheClient.GetAsync<long>(cacheKey).AnyContext();
        return limit.HasValue && limit.Value >= apiThrottleLimit;
    }

    public static int GetMaxEventsPerMonthWithBonus(this Organization organization) {
        if (organization.MaxEventsPerMonth <= 0)
            return -1;

        int bonusEvents = organization.BonusExpiration.HasValue && organization.BonusExpiration > SystemClock.UtcNow ? organization.BonusEventsPerMonth : 0;
        return organization.MaxEventsPerMonth + bonusEvents;
    }

    public static bool IsOverMonthlyLimit(this Organization organization) {
        if (organization.MaxEventsPerMonth < 0)
            return false;

        return organization.GetCurrentUsage().Total >= organization.GetMaxEventsPerMonthWithBonus();
    }

    public static bool HasOverage(this Organization organization, DateTime date) {
        return organization.OverageHours.Any(o => o.Date == date.StartOfHour());
    }

    public static UsageInfo GetOverage(this Organization organization, DateTime date) {
        var usage = organization.OverageHours.FirstOrDefault(o => o.Date == date.StartOfHour());
        if (usage != null)
            return usage;

        usage = new UsageInfo {
            Date = date.StartOfHour(),
            Limit = organization.GetMaxEventsPerMonthWithBonus()
        };
        organization.OverageHours.Add(usage);

        return usage;
    }

    public static UsageInfo GetCurrentUsage(this Organization organization) {
        return organization.GetUsage(SystemClock.UtcNow);
    }

    public static UsageInfo GetUsage(this Organization organization, DateTime date) {
        var usage = organization.Usage.FirstOrDefault(o => o.Date == date.StartOfMonth());
        if (usage != null)
            return usage;

        usage = new UsageInfo {
            Date = date.StartOfMonth(),
            Limit = organization.GetMaxEventsPerMonthWithBonus()
        };
        organization.Usage.Add(usage);

        return usage;
    }
}
