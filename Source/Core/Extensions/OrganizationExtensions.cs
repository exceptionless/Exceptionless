using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models;
using Foundatio.Caching;

namespace Exceptionless.Core.Extensions {
    public static class OrganizationExtensions {
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

            // allow any single hour to have 5 times the monthly limit converted to hours
            return (int)Math.Ceiling(organization.GetMaxEventsPerMonthWithBonus() / 730d * 5d);
        }

        public static int GetMaxEventsPerMonthWithBonus(this Organization organization) {
            if (organization.MaxEventsPerMonth <= 0)
                return -1;

            int bonusEvents = organization.BonusExpiration.HasValue && organization.BonusExpiration > DateTime.Now ? organization.BonusEventsPerMonth : 0;

            return organization.MaxEventsPerMonth + bonusEvents;
        } 

        public static bool IsOverRequestLimit(this Organization organization, ICacheClient cacheClient, int apiThrottleLimit) {
            var cacheKey = String.Concat("api", ":", organization.Id, ":", DateTime.UtcNow.Floor(TimeSpan.FromMinutes(15)).Ticks);
            int? limit = cacheClient.Get<int?>(cacheKey);
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
            return usageInfo != null ? usageInfo.Total : 0;
        }

        public static int GetCurrentHourlyBlocked(this Organization organization) { 
            var date = DateTime.UtcNow.Floor(TimeSpan.FromHours(1));
            var usageInfo = organization.OverageHours.FirstOrDefault(o => o.Date == date);
            return usageInfo != null ? usageInfo.Blocked : 0;
        }

        public static int GetCurrentHourlyTooBig(this Organization organization) {
            var date = DateTime.UtcNow.Floor(TimeSpan.FromHours(1));
            var usageInfo = organization.OverageHours.FirstOrDefault(o => o.Date == date);
            return usageInfo != null ? usageInfo.TooBig : 0;
        }

        public static int GetCurrentMonthlyTotal(this Organization organization) {
            var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usageInfo = organization.Usage.FirstOrDefault(o => o.Date == date);
            return usageInfo != null ? usageInfo.Total : 0;
        }

        public static int GetCurrentMonthlyBlocked(this Organization organization) {
            var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usageInfo = organization.Usage.FirstOrDefault(o => o.Date == date);
            return usageInfo != null ? usageInfo.Blocked : 0;
        }

        public static int GetCurrentMonthlyTooBig(this Organization organization) {
            var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usageInfo = organization.Usage.FirstOrDefault(o => o.Date == date);
            return usageInfo != null ? usageInfo.TooBig : 0;
        }

        public static void SetHourlyOverage(this Organization organization, long total, long blocked, long tooBig) {
            var date = DateTime.UtcNow.Floor(TimeSpan.FromHours(1));
            organization.OverageHours.SetUsage(date, (int)total, (int)blocked, (int)tooBig, organization.GetHourlyEventLimit(), TimeSpan.FromDays(32));
        }

        public static void SetMonthlyUsage(this Organization organization, long total, long blocked, long tooBig) {
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