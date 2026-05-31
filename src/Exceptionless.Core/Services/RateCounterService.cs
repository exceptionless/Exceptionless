using Foundatio.Caching;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

/// <summary>
/// Cache-backed 1-minute bucket counter service for rate notification evaluation.
/// Keys:
///   rate:v1:count:{epochMinute}:{counterKey}  — TTL 3h
///   rate:v1:active:{epochMinute}              — TTL 3h (list of counter keys)
///   rate:v1:cooldown:{ruleId}:{subjectKey}    — TTL = cooldown + 10min
/// </summary>
public class RateCounterService
{
    private readonly ICacheClient _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RateCounterService> _logger;

    private static readonly TimeSpan BucketTtl = TimeSpan.FromHours(3);

    public RateCounterService(ICacheClient cache, TimeProvider timeProvider, ILoggerFactory loggerFactory)
    {
        _cache = cache;
        _timeProvider = timeProvider;
        _logger = loggerFactory.CreateLogger<RateCounterService>();
    }

    /// <summary>Increments the 1-minute bucket counter for the given counter key at the current UTC minute.</summary>
    public async Task IncrementAsync(string counterKey, CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        long epochMinute = GetEpochMinute(now);

        string countKey = GetCountKey(epochMinute, counterKey);
        await _cache.IncrementAsync(countKey, 1, BucketTtl);

        string activeKey = GetActiveKey(epochMinute);
        await _cache.ListAddAsync(activeKey, counterKey, BucketTtl);
    }

    /// <summary>Sums all 1-minute bucket counts for the given counter key in the range [fromUtc, toUtc].</summary>
    public async Task<long> SumBucketsAsync(string counterKey, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        long fromMinute = GetEpochMinute(fromUtc);
        long toMinute = GetEpochMinute(toUtc);

        long total = 0;
        for (long minute = fromMinute; minute <= toMinute; minute++)
        {
            string key = GetCountKey(minute, counterKey);
            var value = await _cache.GetAsync<long>(key);
            if (value.HasValue)
                total += value.Value;
        }

        return total;
    }

    /// <summary>Returns all counter keys that were active during the given minute.</summary>
    public async Task<IReadOnlyList<string>> GetActiveCounterKeysAsync(DateTime minute, CancellationToken ct = default)
    {
        long epochMinute = GetEpochMinute(minute);
        string activeKey = GetActiveKey(epochMinute);

        var result = await _cache.GetListAsync<string>(activeKey);
        if (!result.HasValue || result.Value is null)
            return Array.Empty<string>();

        return result.Value.Where(k => k is not null).Distinct().ToList()!;
    }

    /// <summary>Returns true if the rule/subject is currently on cooldown.</summary>
    public Task<bool> IsOnCooldownAsync(string ruleId, string subjectKey, CancellationToken ct = default)
    {
        string key = GetCooldownKey(ruleId, subjectKey);
        return _cache.ExistsAsync(key);
    }

    /// <summary>Sets a cooldown for the given rule/subject combination.</summary>
    public Task SetCooldownAsync(string ruleId, string subjectKey, TimeSpan duration, CancellationToken ct = default)
    {
        string key = GetCooldownKey(ruleId, subjectKey);
        // TTL = duration + 10 minutes buffer
        return _cache.SetAsync(key, true, duration.Add(TimeSpan.FromMinutes(10)));
    }

    // ---- Key helpers ----

    private static long GetEpochMinute(DateTime utc)
        => (long)(utc - DateTime.UnixEpoch).TotalMinutes;

    private static string GetCountKey(long epochMinute, string counterKey)
        => $"rate:v1:count:{epochMinute}:{counterKey}";

    private static string GetActiveKey(long epochMinute)
        => $"rate:v1:active:{epochMinute}";

    private static string GetCooldownKey(string ruleId, string subjectKey)
        => $"rate:v1:cooldown:{ruleId}:{subjectKey}";
}
