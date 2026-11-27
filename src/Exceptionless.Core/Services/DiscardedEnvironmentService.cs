using Exceptionless.Core.Models;
using Foundatio.Caching;

namespace Exceptionless.Core.Services;

/// <summary>
/// Remembers which environment names recently had events discarded for a project so the owner can see them and allow them.
/// Ingestion acknowledges events before they are processed, so clients never learn about these discards.
/// </summary>
/// <remarks>
/// Everything lives in the cache and expires. Each name has a first-seen display casing, a last-seen time, and one counter per UTC day
/// so the reported count covers exactly the retention window. Writes are grouped by name, so a batch costs a handful of cache calls.
/// The number of tracked names per project is capped; names beyond the cap are not tracked until older ones expire. The cap is checked
/// without a lock, so concurrent batches can briefly exceed it by a few names.
/// </remarks>
public class DiscardedEnvironmentService
{
    public const int RetentionDays = 7;
    public const int MaximumNamesPerProject = 50;
    public const int MaximumOtherNamesPerProject = 100;

    private const string UnspecifiedBucket = "unspecified";
    private const string NamePrefix = "name:";
    private const string OtherBucket = "other";
    private static readonly TimeSpan _retention = TimeSpan.FromDays(RetentionDays);
    private static readonly TimeSpan _counterRetention = TimeSpan.FromDays(RetentionDays + 1);

    private readonly ICacheClient _cache;
    private readonly TimeProvider _timeProvider;

    public DiscardedEnvironmentService(ICacheClient cache, TimeProvider timeProvider)
    {
        _cache = cache;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Records discarded events. A null or empty environment is counted in the bucket for events without an environment.
    /// </summary>
    public async Task RecordAsync(string projectId, IEnumerable<string?> environments)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);

        var groups = new Dictionary<string, (string? Name, int Count)>(StringComparer.Ordinal);
        foreach (string? environment in environments)
        {
            string bucket = GetBucket(environment);
            groups[bucket] = groups.TryGetValue(bucket, out var group)
                ? (group.Name, group.Count + 1)
                : (String.IsNullOrEmpty(environment) ? null : environment, 1);
        }

        if (groups.Count == 0)
            return;

        string namesKey = GetNamesKey(projectId);
        var tracked = await _cache.GetListAsync<string>(namesKey);
        var trackedBuckets = tracked.HasValue ? new HashSet<string>(tracked.Value, StringComparer.Ordinal) : [];

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        string day = GetDay(utcNow);
        var otherBuckets = new List<string>();
        int otherEvents = 0;
        foreach ((string bucket, (string? name, int count)) in groups)
        {
            if (!trackedBuckets.Contains(bucket) && trackedBuckets.Count >= MaximumNamesPerProject)
            {
                otherBuckets.Add(bucket);
                otherEvents += count;
                continue;
            }

            await Task.WhenAll(
                _cache.ListAddAsync(namesKey, bucket, _retention),
                _cache.AddAsync(GetDisplayKey(projectId, bucket), name ?? String.Empty, _retention),
                _cache.SetAsync(GetLastSeenKey(projectId, bucket), utcNow, _retention),
                _cache.IncrementAsync(GetCountKey(projectId, bucket, day), count, _counterRetention)
            );

            trackedBuckets.Add(bucket);
        }

        if (otherEvents > 0)
            await RecordOtherAsync(projectId, otherBuckets, otherEvents, day);
    }

    private async Task RecordOtherAsync(string projectId, List<string> buckets, int events, string day)
    {
        // Names past the limit are not tracked, but the page still reports that events were dropped and roughly from how many names.
        string otherNamesKey = GetOtherNamesKey(projectId);
        var known = await _cache.GetListAsync<string>(otherNamesKey);
        var knownBuckets = known.HasValue ? new HashSet<string>(known.Value, StringComparer.Ordinal) : [];
        var newBuckets = buckets.Where(b => !knownBuckets.Contains(b)).Take(Math.Max(MaximumOtherNamesPerProject - knownBuckets.Count, 0)).ToList();

        var tasks = new List<Task> { _cache.IncrementAsync(GetCountKey(projectId, OtherBucket, day), events, _counterRetention) };
        if (newBuckets.Count > 0)
            tasks.Add(_cache.ListAddAsync(otherNamesKey, newBuckets, _retention));

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Gets the discarded names, most discarded first, and the events from names that were not tracked.
    /// </summary>
    public async Task<DiscardedEnvironmentSummary> GetAsync(string projectId)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var days = Enumerable.Range(0, RetentionDays).Select(offset => GetDay(utcNow.AddDays(-offset))).ToList();

        var otherCounts = await _cache.GetAllAsync<long>(days.Select(d => GetCountKey(projectId, OtherBucket, d)));
        long otherEvents = otherCounts.Values.Sum(v => v.HasValue ? v.Value : 0);
        var otherNames = otherEvents > 0 ? await _cache.GetListAsync<string>(GetOtherNamesKey(projectId)) : default;
        int otherNameCount = otherNames is { HasValue: true } ? otherNames.Value.Count : 0;

        var tracked = await _cache.GetListAsync<string>(GetNamesKey(projectId));
        var environments = tracked.HasValue && tracked.Value.Count > 0 ? await GetTrackedAsync(projectId, tracked.Value, days) : [];

        return new DiscardedEnvironmentSummary
        {
            Environments = environments,
            OtherEvents = otherEvents,
            OtherNames = otherNameCount,
            OtherNamesCapped = otherNameCount >= MaximumOtherNamesPerProject
        };
    }

    private async Task<IReadOnlyCollection<DiscardedEnvironment>> GetTrackedAsync(string projectId, ICollection<string> tracked, List<string> days)
    {
        var buckets = tracked.Distinct(StringComparer.Ordinal).ToList();

        var displayNames = await _cache.GetAllAsync<string>(buckets.Select(b => GetDisplayKey(projectId, b)));
        var lastSeen = await _cache.GetAllAsync<DateTime>(buckets.Select(b => GetLastSeenKey(projectId, b)));
        var counts = await _cache.GetAllAsync<long>(buckets.SelectMany(b => days.Select(d => GetCountKey(projectId, b, d))));

        var results = new List<DiscardedEnvironment>(buckets.Count);
        foreach (string bucket in buckets)
        {
            long count = days.Sum(day => counts.TryGetValue(GetCountKey(projectId, bucket, day), out var value) && value.HasValue ? value.Value : 0);
            if (count <= 0)
                continue;

            bool isUnspecified = bucket == UnspecifiedBucket;
            string? name = null;
            if (!isUnspecified)
            {
                name = displayNames.TryGetValue(GetDisplayKey(projectId, bucket), out var display) && display.HasValue && !String.IsNullOrEmpty(display.Value)
                    ? display.Value
                    : bucket[NamePrefix.Length..];
            }

            results.Add(new DiscardedEnvironment
            {
                Key = isUnspecified ? null : bucket[NamePrefix.Length..],
                Name = name,
                Count = count,
                LastSeenUtc = lastSeen.TryGetValue(GetLastSeenKey(projectId, bucket), out var seen) && seen.HasValue ? seen.Value : default
            });
        }

        return results.OrderByDescending(r => r.Count).ThenBy(r => r.Key, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Forgets a name, for example after it was added to the project. A null or empty environment forgets events without an environment.
    /// </summary>
    public async Task RemoveAsync(string projectId, string? environment)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);

        string bucket = GetBucket(environment);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var keys = Enumerable.Range(0, RetentionDays + 1)
            .Select(offset => GetCountKey(projectId, bucket, GetDay(utcNow.AddDays(-offset))))
            .Append(GetDisplayKey(projectId, bucket))
            .Append(GetLastSeenKey(projectId, bucket));

        await _cache.ListRemoveAsync(GetNamesKey(projectId), bucket);
        await _cache.RemoveAllAsync(keys);
    }

    private static string GetBucket(string? environment) => String.IsNullOrEmpty(environment) ? UnspecifiedBucket : NamePrefix + ProjectEnvironment.GetKey(environment);
    private static string GetDay(DateTime utc) => utc.ToString("yyyyMMdd");
    private static string GetPrefix(string projectId) => String.Concat("project:", projectId, ":discarded-environments");
    private static string GetOtherNamesKey(string projectId) => String.Concat(GetPrefix(projectId), ":other-names");
    private static string GetNamesKey(string projectId) => String.Concat(GetPrefix(projectId), ":names");
    private static string GetDisplayKey(string projectId, string bucket) => String.Concat(GetPrefix(projectId), ":display:", bucket);
    private static string GetLastSeenKey(string projectId, string bucket) => String.Concat(GetPrefix(projectId), ":last:", bucket);
    private static string GetCountKey(string projectId, string bucket, string day) => String.Concat(GetPrefix(projectId), ":count:", day, ":", bucket);
}
