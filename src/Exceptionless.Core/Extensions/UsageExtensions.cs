using Exceptionless.Core.Models;
using Exceptionless.DateTimeExtensions;

namespace Exceptionless.Core.Extensions;

public static class UsageExtensions
{
    public static UsageInfo GetOrAddMonthlyUsage(this ICollection<UsageInfo> usages, DateTime dateUtc, int limit)
    {
        var startOfMonth = dateUtc.ToUniversalTime().StartOfMonth();
        var usage = usages.FirstOrDefault(u => u.Date.Year == startOfMonth.Year && u.Date.Month == startOfMonth.Month);
        if (usage is not null)
        {
            return usage;
        }

        usage = new UsageInfo
        {
            Date = startOfMonth,
            Limit = limit
        };
        usages.Add(usage);

        return usage;
    }

    public static ICollection<UsageInfo> MaterializeMonthlyUsage(this IEnumerable<UsageInfo> usages, DateTime startDateUtc, DateTime endDateUtc, int fallbackLimit)
    {
        var materialized = usages.Select(usage => usage with { }).ToList();
        var startOfMonthUtc = startDateUtc.ToUniversalTime().StartOfMonth();
        var endOfMonthUtc = endDateUtc.ToUniversalTime().StartOfMonth();
        if (startOfMonthUtc > endOfMonthUtc)
            return materialized;

        var knownUsages = materialized
            .Where(usage => usage.Limit != 0)
            .OrderBy(usage => usage.Date)
            .ToList();
        int limit = knownUsages
            .LastOrDefault(usage => usage.Date <= startOfMonthUtc)?.Limit
            ?? knownUsages.FirstOrDefault()?.Limit
            ?? fallbackLimit;

        while (startOfMonthUtc <= endOfMonthUtc)
        {
            var usage = materialized.GetOrAddMonthlyUsage(startOfMonthUtc, limit);
            if (usage.Limit == 0)
                usage.Limit = limit;
            else
                limit = usage.Limit;

            startOfMonthUtc = startOfMonthUtc.AddMonths(1);
        }

        return materialized.OrderBy(usage => usage.Date).ToList();
    }

    public static void SetUsage(this ICollection<UsageInfo> usages, DateTime dateUtc, int total, int blocked, int tooBig, int limit, TimeSpan? maxUsageAge, TimeProvider timeProvider)
    {
        var usageInfo = usages.FirstOrDefault(o => o.Date == dateUtc);
        if (usageInfo is null)
        {
            usageInfo = new UsageInfo
            {
                Date = dateUtc,
                Total = total,
                Blocked = blocked,
                Limit = limit,
                TooBig = tooBig
            };
            usages.Add(usageInfo);
        }
        else
        {
            usageInfo.Limit = limit;
            usageInfo.Total = total;
            usageInfo.Blocked = blocked;
            usageInfo.TooBig = tooBig;
        }

        if (!maxUsageAge.HasValue)
            return;

        // remove old usage entries
        foreach (var usage in usages.Where(u => u.Date < timeProvider.GetUtcNow().UtcDateTime.Subtract(maxUsageAge.Value)).ToList())
            usages.Remove(usage);
    }
}
