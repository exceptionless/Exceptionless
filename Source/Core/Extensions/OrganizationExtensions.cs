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
            return organization.MaxErrorsPerMonth / 730 * 5;
        }

        public static void SetHourlyOverage(this Organization organization, long count) {
            var date = DateTime.UtcNow.Floor(TimeSpan.FromHours(1));
            organization.OverageHours.SetOverage(date, (int)count, organization.GetHourlyErrorLimit());
        }

        public static void SetMonthlyOverage(this Organization organization, long count) {
            var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0);
            organization.OverageMonths.SetOverage(date, (int)count, organization.MaxErrorsPerMonth);
        }

        public static void SetOverage(this ICollection<OverageInfo> overages, DateTime date, int count, int limit) {
            var overageInfo = overages.FirstOrDefault(o => o.Date == date);
            if (overageInfo == null) {
                overageInfo = new OverageInfo {
                    Date = date,
                    Count = count,
                    Limit = limit
                };
                overages.Add(overageInfo);
            } else {
                overageInfo.Limit = limit;
                overageInfo.Count = count;
            }
        }
    }
}