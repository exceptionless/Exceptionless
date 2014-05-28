#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using CodeSmith.Core.Extensions;
using Exceptionless.Models;

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

        public static int GetHourlyErrorLimit(this Organization organization) {
            if (organization.MaxErrorsPerMonth <= 0)
                return Int32.MaxValue;

            // allow any single hour to have 5 times the monthly limit converted to hours
            return (int)Math.Ceiling(organization.MaxErrorsPerMonth / 730d * 5d);
        }

        public static bool IsOverMonthlyLimit(this Organization organization) {
            var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usageInfo = organization.Usage.FirstOrDefault(o => o.Date == date);
            return usageInfo != null && usageInfo.Count > organization.MaxErrorsPerMonth;
        }

        public static bool IsOverHourlyLimit(this Organization organization) {
            var date = DateTime.UtcNow.Floor(TimeSpan.FromHours(1));
            var usageInfo = organization.OverageHours.FirstOrDefault(o => o.Date == date);
            return usageInfo != null && usageInfo.Count > organization.GetHourlyErrorLimit();
        }

        public static int GetCurrentMonthlyUsage(this Organization organization) {
            var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usageInfo = organization.Usage.FirstOrDefault(o => o.Date == date);
            return usageInfo != null ? usageInfo.Count : 0;
        }

        public static void SetHourlyOverage(this Organization organization, long count) {
            var date = DateTime.UtcNow.Floor(TimeSpan.FromHours(1));
            organization.OverageHours.SetUsage(date, (int)count, organization.GetHourlyErrorLimit());
        }

        public static void SetMonthlyUsage(this Organization organization, long count) {
            var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            organization.Usage.SetUsage(date, (int)count, organization.MaxErrorsPerMonth);
        }

        public static void SetUsage(this ICollection<UsageInfo> usages, DateTime date, int count, int limit) {
            var usageInfo = usages.FirstOrDefault(o => o.Date == date);
            if (usageInfo == null) {
                usageInfo = new UsageInfo {
                    Date = date,
                    Count = count,
                    Limit = limit
                };
                usages.Add(usageInfo);
            } else {
                usageInfo.Limit = limit;
                usageInfo.Count = count;
            }
        }
    }
}