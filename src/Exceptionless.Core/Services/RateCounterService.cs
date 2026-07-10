using Foundatio.Caching;

namespace Exceptionless.Core.Services;

/// <summary>
/// Cache-backed 1-minute bucket counter service for rate notification evaluation.
/// Keys:
///   rate:v1:count:{epochMinute}:{counterKey}  — TTL 3h
///   rate:v1:active:{epochMinute}              — TTL 3h (list of counter keys)
///   rate:v1:cooldown:{ruleId}:{subjectKey}    — TTL = configured cooldown
///   rate:v1:evaluator:last-minute             — last completed evaluation minute
/// </summary>
public class RateCounterService
{
    private readonly ICacheClient _cache;
    private readonly TimeProvider _timeProvider;

    private static readonly TimeSpan BucketTtl = TimeSpan.FromHours(3);
    private const string LastEvaluatedMinuteKey = "rate:v1:evaluator:last-minute";

    public RateCounterService(ICacheClient cache, TimeProvider timeProvider)
    {
        _cache = cache;
        _timeProvider = timeProvider;
    }

    /// <summary>Increments the 1-minute bucket counter for the given counter key at the current UTC minute.</summary>
    public Task IncrementAsync(string counterKey, CancellationToken ct = default)
        => IncrementAsync([counterKey], ct);

    /// <summary>Increments all matching counters and records their active keys with one list operation.</summary>
    public async Task IncrementAsync(IReadOnlyCollection<string> counterKeys, CancellationToken ct = default)
    {
        if (counterKeys.Count == 0)
            return;

        ct.ThrowIfCancellationRequested();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        long epochMinute = GetEpochMinute(now);
        var distinctCounterKeys = counterKeys.Distinct(StringComparer.Ordinal).ToList();

        var increments = distinctCounterKeys.Select(async counterKey =>
        {
            await _cache.IncrementAsync(GetCountKey(epochMinute, counterKey), 1, BucketTtl);
        });

        string activeKey = GetActiveKey(epochMinute);
        Task updateActiveKeys = _cache.ListAddAsync(activeKey, distinctCounterKeys, BucketTtl);
        await Task.WhenAll(increments.Append(updateActiveKeys));
    }

    /// <summary>Sums all 1-minute bucket counts for the given counter key in the range [fromUtc, toUtc).</summary>
    public async Task<long> SumBucketsAsync(string counterKey, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        long fromMinute = GetEpochMinute(fromUtc);
        long toMinute = GetEpochMinute(toUtc);
        if (fromMinute >= toMinute)
            return 0;

        var keys = Enumerable.Range(0, checked((int)(toMinute - fromMinute)))
            .Select(offset => GetCountKey(fromMinute + offset, counterKey))
            .ToList();
        var values = await _cache.GetAllAsync<long>(keys);

        return values.Values.Where(value => value.HasValue).Sum(value => value.Value);
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

    /// <summary>Atomically claims the cooldown for a rule/subject combination.</summary>
    public Task<bool> TrySetCooldownAsync(string ruleId, string subjectKey, TimeSpan duration, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return _cache.AddAsync(GetCooldownKey(ruleId, subjectKey), true, duration);
    }

    public Task RemoveCooldownAsync(string ruleId, string subjectKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return _cache.RemoveAsync(GetCooldownKey(ruleId, subjectKey));
    }

    public async Task<DateTime?> GetLastEvaluatedMinuteAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var value = await _cache.GetAsync<long>(LastEvaluatedMinuteKey);
        return value.HasValue ? DateTime.UnixEpoch.AddMinutes(value.Value) : null;
    }

    public Task SetLastEvaluatedMinuteAsync(DateTime minute, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return _cache.SetAsync(LastEvaluatedMinuteKey, GetEpochMinute(minute), BucketTtl);
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
