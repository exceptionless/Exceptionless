using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Foundatio.Utility;

namespace Exceptionless.Core.Extensions {
    public static class UsageExtensions {
        public static void SetUsage(this ICollection<UsageInfo> usages, DateTime dateUtc, int total, int blocked, int tooBig, int limit, TimeSpan? maxUsageAge = null) {
            var usageInfo = usages.FirstOrDefault(o => o.Date == dateUtc);
            if (usageInfo == null) {
                usageInfo = new UsageInfo {
                    Date = dateUtc,
                    Total = total,
                    Blocked = blocked,
                    Limit = limit,
                    TooBig = tooBig
                };
                usages.Add(usageInfo);
            }
            else {
                usageInfo.Limit = limit;
                usageInfo.Total = total;
                usageInfo.Blocked = blocked;
                usageInfo.TooBig = tooBig;
            }

            if (!maxUsageAge.HasValue)
                return;

            // remove old usage entries
            foreach (var usage in usages.Where(u => u.Date < SystemClock.UtcNow.Subtract(maxUsageAge.Value)).ToList())
                usages.Remove(usage);
        }
    }
}