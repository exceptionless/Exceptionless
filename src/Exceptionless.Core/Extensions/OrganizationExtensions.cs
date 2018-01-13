using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models;
using Foundatio.Caching;
using Foundatio.Utility;

namespace Exceptionless.Core.Extensions {
    public static class OrganizationExtensions {
        public static Invite GetInvite(this Organization organization, string token) {
            if (organization == null || String.IsNullOrEmpty(token))
                return null;

            return organization.Invites.FirstOrDefault(i => String.Equals(i.Token, token, StringComparison.OrdinalIgnoreCase));
        }

        public static DateTime GetRetentionUtcCutoff(this Organization organization, Project project) {
            return organization.GetRetentionUtcCutoff(project.CreatedUtc.SafeSubtract(TimeSpan.FromDays(3)));
        }

        public static DateTime GetRetentionUtcCutoff(this Organization organization, Stack stack) {
            return organization.GetRetentionUtcCutoff(stack.FirstOccurrence);
        }

        public static DateTime GetRetentionUtcCutoff(this Organization organization, DateTime? oldestPossibleEventAge = null) {
            // NOTE: We allow you to submit events 3 days before your creation date.
            var oldestPossibleOrganizationEventAge = organization.CreatedUtc.Date.SafeSubtract(TimeSpan.FromDays(3));
            if (!oldestPossibleEventAge.HasValue || oldestPossibleEventAge.Value.IsBefore(oldestPossibleOrganizationEventAge))
                oldestPossibleEventAge = oldestPossibleOrganizationEventAge;

            int retentionDays = organization.RetentionDays > 0 ? organization.RetentionDays : Settings.Current.MaximumRetentionDays;
            var retentionDate = retentionDays <= 0 ? oldestPossibleEventAge.Value : SystemClock.UtcNow.Date.AddDays(-retentionDays);
            return retentionDate.IsAfter(oldestPossibleEventAge.Value) ? retentionDate : oldestPossibleEventAge.Value;
        }

        public static DateTime GetRetentionUtcCutoff(this IReadOnlyCollection<Organization> organizations) {
            return organizations.Count > 0 ? organizations.Min(o => o.GetRetentionUtcCutoff()) : DateTime.MinValue;
        }

        public static void RemoveSuspension(this Organization organization) {
            organization.IsSuspended = false;
            organization.SuspensionDate = null;
            organization.SuspensionCode = null;
            organization.SuspensionNotes = null;
            organization.SuspendedByUserId = null;
        }

        public static int GetHourlyEventLimit(this Organization organization) {
            if (organization.MaxEventsPerMonth <= 0)
                return Int32.MaxValue;

            int eventsLeftInMonth = organization.GetMaxEventsPerMonthWithBonus() - (organization.GetCurrentMonthlyTotal() - organization.GetCurrentMonthlyBlocked());
            if (eventsLeftInMonth < 0)
                return 0;

            if (organization.PlanId == BillingManager.FreePlan.Id)
                return eventsLeftInMonth;

            var utcNow = SystemClock.UtcNow;
            double hoursLeftInMonth = (utcNow.EndOfMonth() - utcNow).TotalHours;
            if (hoursLeftInMonth < 10.0)
                return eventsLeftInMonth;

            return (int)Math.Ceiling(eventsLeftInMonth / hoursLeftInMonth * 10d);
        }

        public static int GetMaxEventsPerMonthWithBonus(this Organization organization) {
            if (organization.MaxEventsPerMonth <= 0)
                return -1;

            int bonusEvents = organization.BonusExpiration.HasValue && organization.BonusExpiration > SystemClock.UtcNow ? organization.BonusEventsPerMonth : 0;
            return organization.MaxEventsPerMonth + bonusEvents;
        }

        public static async Task<bool> IsOverRequestLimitAsync(string organizationId, ICacheClient cacheClient, int apiThrottleLimit) {
            if (apiThrottleLimit == Int32.MaxValue)
                return false;

            string cacheKey = String.Concat("api", ":", organizationId, ":", SystemClock.UtcNow.Floor(TimeSpan.FromMinutes(15)).Ticks);
            var limit = await cacheClient.GetAsync<long>(cacheKey).AnyContext();
            return limit.HasValue && limit.Value >= apiThrottleLimit;
        }

        public static bool IsOverMonthlyLimit(this Organization organization) {
            if (organization.MaxEventsPerMonth < 0)
                return false;

            var date = new DateTime(SystemClock.UtcNow.Year, SystemClock.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usageInfo = organization.Usage.FirstOrDefault(o => o.Date == date);
            return usageInfo != null && (usageInfo.Total - usageInfo.Blocked) >= organization.GetMaxEventsPerMonthWithBonus();
        }

        public static bool IsOverHourlyLimit(this Organization organization) {
            var date = SystemClock.UtcNow.Floor(TimeSpan.FromHours(1));
            var usageInfo = organization.OverageHours.FirstOrDefault(o => o.Date == date);
            return usageInfo != null && usageInfo.Total > organization.GetHourlyEventLimit();
        }

       public static int GetCurrentHourlyTotal(this Organization organization) {
            var date = SystemClock.UtcNow.Floor(TimeSpan.FromHours(1));
            var usageInfo = organization.OverageHours.FirstOrDefault(o => o.Date == date);
            return usageInfo?.Total ?? 0;
        }

        public static int GetCurrentHourlyBlocked(this Organization organization) {
            var date = SystemClock.UtcNow.Floor(TimeSpan.FromHours(1));
            var usageInfo = organization.OverageHours.FirstOrDefault(o => o.Date == date);
            return usageInfo?.Blocked ?? 0;
        }

        public static int GetCurrentHourlyTooBig(this Organization organization) {
            var date = SystemClock.UtcNow.Floor(TimeSpan.FromHours(1));
            var usageInfo = organization.OverageHours.FirstOrDefault(o => o.Date == date);
            return usageInfo?.TooBig ?? 0;
        }

        public static int GetCurrentMonthlyTotal(this Organization organization) {
            var date = new DateTime(SystemClock.UtcNow.Year, SystemClock.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usageInfo = organization.Usage.FirstOrDefault(o => o.Date == date);
            return usageInfo?.Total ?? 0;
        }

        public static int GetCurrentMonthlyBlocked(this Organization organization) {
            var date = new DateTime(SystemClock.UtcNow.Year, SystemClock.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usageInfo = organization.Usage.FirstOrDefault(o => o.Date == date);
            return usageInfo?.Blocked ?? 0;
        }

        public static int GetCurrentMonthlyTooBig(this Organization organization) {
            var date = new DateTime(SystemClock.UtcNow.Year, SystemClock.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usageInfo = organization.Usage.FirstOrDefault(o => o.Date == date);
            return usageInfo?.TooBig ?? 0;
        }

        public static void SetHourlyOverage(this Organization organization, double total, double blocked, double tooBig) {
            var date = SystemClock.UtcNow.Floor(TimeSpan.FromHours(1));
            organization.OverageHours.SetUsage(date, (int)total, (int)blocked, (int)tooBig, organization.GetHourlyEventLimit(), TimeSpan.FromDays(3));
        }

        public static void SetMonthlyUsage(this Organization organization, double total, double blocked, double tooBig) {
            var date = new DateTime(SystemClock.UtcNow.Year, SystemClock.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            organization.Usage.SetUsage(date, (int)total, (int)blocked, (int)tooBig, organization.GetMaxEventsPerMonthWithBonus(), TimeSpan.FromDays(366));
        }
    }
}