using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models;
using Foundatio.Caching;

namespace Exceptionless.Core.Extensions {
    public static class OrganizationExtensions {
        public static Invite GetInvite(this Organization organization, string token) {
            if (organization == null || String.IsNullOrEmpty(token))
                return null;

            return organization.Invites.FirstOrDefault(i => String.Equals(i.Token, token, StringComparison.OrdinalIgnoreCase));
        }

        public static DateTime GetRetentionUtcCutoff(this Organization organization) {
            return organization.RetentionDays <= 0 ? DateTime.MinValue : DateTime.UtcNow.Date.AddDays(-organization.RetentionDays);
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

            // allow any single hour to have 10 times the monthly limit converted to hours
            return (int)Math.Ceiling(organization.GetMaxEventsPerMonthWithBonus() / 730d * 10d);
        }

        public static int GetMaxEventsPerMonthWithBonus(this Organization organization) {
            if (organization.MaxEventsPerMonth <= 0)
                return -1;

            int bonusEvents = organization.BonusExpiration.HasValue && organization.BonusExpiration > DateTime.UtcNow ? organization.BonusEventsPerMonth : 0;

            return organization.MaxEventsPerMonth + bonusEvents;
        } 

        public static Task<bool> IsOverRequestLimitAsync(this Organization organization, ICacheClient cacheClient, int apiThrottleLimit) {
            return IsOverRequestLimitAsync(organization.Id, cacheClient, apiThrottleLimit);
        }

        public static async Task<bool> IsOverRequestLimitAsync(string organizationId, ICacheClient cacheClient, int apiThrottleLimit) {
            var cacheKey = String.Concat("api", ":", organizationId, ":", DateTime.UtcNow.Floor(TimeSpan.FromMinutes(15)).Ticks);
            var limit = await cacheClient.GetAsync<long>(cacheKey).AnyContext();
            return limit.HasValue && limit.Value >= apiThrottleLimit;
        }

        public static bool IsOverMonthlyLimit(this Organization organization) {
            if (organization.MaxEventsPerMonth < 0)
                return false;
            
            var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usageInfo = organization.Usage.FirstOrDefault(o => o.Date == date);
            return usageInfo != null && (usageInfo.Total - usageInfo.Blocked) >= organization.GetMaxEventsPerMonthWithBonus();
        }

        public static bool IsOverHourlyLimit(this Organization organization) {
            var date = DateTime.UtcNow.Floor(TimeSpan.FromHours(1));
            var usageInfo = organization.OverageHours.FirstOrDefault(o => o.Date == date);
            return usageInfo != null && usageInfo.Total > organization.GetHourlyEventLimit();
        }

       public static int GetCurrentHourlyTotal(this Organization organization) { 
            var date = DateTime.UtcNow.Floor(TimeSpan.FromHours(1));
            var usageInfo = organization.OverageHours.FirstOrDefault(o => o.Date == date);
            return usageInfo?.Total ?? 0;
        }

        public static int GetCurrentHourlyBlocked(this Organization organization) { 
            var date = DateTime.UtcNow.Floor(TimeSpan.FromHours(1));
            var usageInfo = organization.OverageHours.FirstOrDefault(o => o.Date == date);
            return usageInfo?.Blocked ?? 0;
        }

        public static int GetCurrentHourlyTooBig(this Organization organization) {
            var date = DateTime.UtcNow.Floor(TimeSpan.FromHours(1));
            var usageInfo = organization.OverageHours.FirstOrDefault(o => o.Date == date);
            return usageInfo?.TooBig ?? 0;
        }

        public static int GetCurrentMonthlyTotal(this Organization organization) {
            var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usageInfo = organization.Usage.FirstOrDefault(o => o.Date == date);
            return usageInfo?.Total ?? 0;
        }

        public static int GetCurrentMonthlyBlocked(this Organization organization) {
            var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usageInfo = organization.Usage.FirstOrDefault(o => o.Date == date);
            return usageInfo?.Blocked ?? 0;
        }

        public static int GetCurrentMonthlyTooBig(this Organization organization) {
            var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usageInfo = organization.Usage.FirstOrDefault(o => o.Date == date);
            return usageInfo?.TooBig ?? 0;
        }

        public static void SetHourlyOverage(this Organization organization, double total, double blocked, double tooBig) {
            var date = DateTime.UtcNow.Floor(TimeSpan.FromHours(1));
            organization.OverageHours.SetUsage(date, (int)total, (int)blocked, (int)tooBig, organization.GetHourlyEventLimit(), TimeSpan.FromDays(32));
        }

        public static void SetMonthlyUsage(this Organization organization, double total, double blocked, double tooBig) {
            var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            organization.Usage.SetUsage(date, (int)total, (int)blocked, (int)tooBig, organization.GetMaxEventsPerMonthWithBonus(), TimeSpan.FromDays(366));
        }

        public static void SetUsage(this ICollection<UsageInfo> usages, DateTime date, int total, int blocked, int tooBig, int limit, TimeSpan? maxUsageAge = null) {
            var usageInfo = usages.FirstOrDefault(o => o.Date == date);
            if (usageInfo == null) {
                usageInfo = new UsageInfo {
                    Date = date,
                    Total = total,
                    Blocked = blocked,
                    Limit = limit,
                    TooBig = tooBig
                };
                usages.Add(usageInfo);
            } else {
                usageInfo.Limit = limit;
                usageInfo.Total = total;
                usageInfo.Blocked = blocked;
                usageInfo.TooBig = tooBig;
            }

            if (!maxUsageAge.HasValue)
                return;

            // remove old usage entries
            foreach (var usage in usages.Where(u => u.Date < DateTime.UtcNow.Subtract(maxUsageAge.Value)).ToList())
                usages.Remove(usage);
        }
    }
}