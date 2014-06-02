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
using System.Web.Razor.Parser.SyntaxTree;
using CodeSmith.Core.Extensions;
using Exceptionless.Models;
using NLog.LayoutRenderers;

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
            return usageInfo != null && usageInfo.Total > organization.MaxErrorsPerMonth;
        }

        public static bool IsOverHourlyLimit(this Organization organization) {
            var date = DateTime.UtcNow.Floor(TimeSpan.FromHours(1));
            var usageInfo = organization.OverageHours.FirstOrDefault(o => o.Date == date);
            return usageInfo != null && usageInfo.Total > organization.GetHourlyErrorLimit();
        }

        public static int GetCurrentMonthlyTotal(this Organization organization) {
            var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usageInfo = organization.Usage.FirstOrDefault(o => o.Date == date);
            return usageInfo != null ? usageInfo.Total : 0;
        }

        public static int GetCurrentMonthlyAccepted(this Organization organization) {
            var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usageInfo = organization.Usage.FirstOrDefault(o => o.Date == date);
            return usageInfo != null ? usageInfo.Accepted : 0;
        }

        public static void SetHourlyOverage(this Organization organization, long total, long accepted) {
            var date = DateTime.UtcNow.Floor(TimeSpan.FromHours(1));
            organization.OverageHours.SetUsage(date, (int)total, (int)accepted, organization.GetHourlyErrorLimit());
        }

        public static void SetMonthlyUsage(this Organization organization, long total, long accepted) {
            var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            organization.Usage.SetUsage(date, (int)total, (int)accepted, organization.MaxErrorsPerMonth);
        }

        public static void SetUsage(this ICollection<UsageInfo> usages, DateTime date, int total, int accepted, int limit) {
            var usageInfo = usages.FirstOrDefault(o => o.Date == date);
            if (usageInfo == null) {
                usageInfo = new UsageInfo {
                    Date = date,
                    Total = total,
                    Accepted = accepted,
                    Limit = limit
                };
                usages.Add(usageInfo);
            } else {
                usageInfo.Limit = limit;
                usageInfo.Total = total;
                usageInfo.Accepted = accepted;
            }
        }
    }
}