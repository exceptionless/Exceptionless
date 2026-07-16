using Foundatio.Caching;
using Foundatio.Lock;

namespace Exceptionless.Core.Services;

public sealed record IngestionStackUsage(
    string EventId,
    string OrganizationId,
    string ProjectId,
    string StackId,
    DateTime OccurrenceDateUtc);

public sealed record StackUsageSummary(
    string OrganizationId,
    string ProjectId,
    string StackId,
    DateTime MinimumOccurrenceDateUtc,
    DateTime MaximumOccurrenceDateUtc,
    int Count);

public sealed record StackUsageClaim(
    string OrganizationId,
    string ProjectId,
    string StackId,
    DateTime MinimumOccurrenceDateUtc,
    DateTime MaximumOccurrenceDateUtc,
    int Count,
    long SettlementSequence,
    long LeaseToken);

public interface IIngestionStackUsageStore
{
    Task<IReadOnlyCollection<StackUsageSummary>> SettleAsync(
        IReadOnlyCollection<IngestionStackUsage> usages,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<StackUsageClaim>> ClaimPendingAsync(
        int maximumCount,
        CancellationToken cancellationToken = default);

    Task AcknowledgeAsync(
        IReadOnlyCollection<StackUsageClaim> claims,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Process-local statistics settlement used with the in-memory cache provider. The private
/// completion ledger is the authority if publishing the shared processing-state bit fails, so
/// a retry repairs the bit without applying the stack usage a second time.
/// </summary>
public sealed class InMemoryIngestionStackUsageStore(
    ICacheClient cache,
    ILockProvider lockProvider,
    AppOptions options,
    TimeProvider timeProvider) : IIngestionStackUsageStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, DateTimeOffset> _completedEvents = new(StringComparer.Ordinal);
    private readonly Dictionary<StackUsageKey, MutableStackUsage> _pendingUsages = [];
    private readonly Dictionary<StackUsageKey, InFlightStackUsage> _inFlightUsages = [];
    private long _nextSettlementSequence;
    private int _takeCursor = -1;

    public async Task<IReadOnlyCollection<StackUsageSummary>> SettleAsync(
        IReadOnlyCollection<IngestionStackUsage> usages,
        CancellationToken cancellationToken = default)
    {
        var normalized = IngestionStackUsageStore.Normalize(usages);
        if (normalized.Count == 0)
        {
            return [];
        }

        string[] lockKeys = normalized
            .Select(usage => IngestionStackUsageStore.GetStateLockKey(usage.ProjectId, usage.EventId))
            .ToArray();
        await using ILock stateLocks = await lockProvider.AcquireAsync(lockKeys, cancellationToken: cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            RemoveExpiredCompletions(now);

            string[] stateKeys = normalized
                .Select(usage => IngestionStackUsageStore.GetStateKey(usage.ProjectId, usage.EventId))
                .ToArray();
            var states = await cache.GetAllAsync<int>(stateKeys);
            var newlySettled = new List<IngestionStackUsage>(normalized.Count);
            var stateUpdates = new Dictionary<string, int>(normalized.Count, StringComparer.Ordinal);
            DateTimeOffset expiresAt = now.Add(options.EventIngestionV3.IdempotencyWindow);

            foreach (var usage in normalized)
            {
                string stateKey = IngestionStackUsageStore.GetStateKey(usage.ProjectId, usage.EventId);
                var cachedState = states[stateKey];
                int state = cachedState.HasValue ? cachedState.Value : 0;
                bool isCompleted = _completedEvents.ContainsKey(stateKey)
                    || (state & IngestionStackUsageStore.StatisticsStageFlag) != 0;

                if (!isCompleted)
                {
                    _completedEvents[stateKey] = expiresAt;
                    AddPendingUsage(usage);
                    newlySettled.Add(usage);
                }
                else if (!_completedEvents.ContainsKey(stateKey))
                {
                    _completedEvents[stateKey] = expiresAt;
                }

                if ((state & IngestionStackUsageStore.StatisticsStageFlag) == 0)
                {
                    stateUpdates[stateKey] = state | IngestionStackUsageStore.StatisticsStageFlag;
                }
            }

            if (stateUpdates.Count > 0)
            {
                int completedCount = await cache.SetAllAsync(stateUpdates, options.EventIngestionV3.IdempotencyWindow);
                if (completedCount != stateUpdates.Count)
                {
                    throw new InvalidOperationException("Unable to record ingestion stack-statistics completion.");
                }
            }

            return IngestionStackUsageStore.Summarize(newlySettled);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyCollection<StackUsageClaim>> ClaimPendingAsync(
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            TimeSpan claimLease = options.EventIngestionV3.StackUsageClaimLease > TimeSpan.Zero
                ? options.EventIngestionV3.StackUsageClaimLease
                : TimeSpan.FromMinutes(1);
            DateTimeOffset leaseExpiresAt = now.Add(claimLease);
            var result = new List<StackUsageClaim>(Math.Min(maximumCount, _pendingUsages.Count + _inFlightUsages.Count));

            foreach (var pair in _inFlightUsages
                         .Where(pair => pair.Value.LeaseExpiresAt <= now)
                         .OrderBy(pair => pair.Value.LeaseExpiresAt)
                         .ThenBy(pair => pair.Key.ProjectId, StringComparer.Ordinal)
                         .ThenBy(pair => pair.Key.StackId, StringComparer.Ordinal)
                         .Take(maximumCount))
            {
                pair.Value.LeaseExpiresAt = leaseExpiresAt;
                result.Add(pair.Value.ToClaim(pair.Key));
            }

            if (result.Count >= maximumCount)
            {
                return result;
            }

            StackUsageKey[][] partitions = _pendingUsages.Keys
                .Where(key => !_inFlightUsages.ContainsKey(key))
                .GroupBy(key => (key.OrganizationId, key.ProjectId))
                .OrderBy(group => group.Key.ProjectId, StringComparer.Ordinal)
                .Select(group => group.OrderBy(key => key.StackId, StringComparer.Ordinal).ToArray())
                .ToArray();
            if (partitions.Length == 0)
            {
                return result;
            }

            int startIndex = (int)((uint)Interlocked.Increment(ref _takeCursor) % (uint)partitions.Length);
            partitions = Enumerable.Range(0, partitions.Length)
                .Select(offset => partitions[(startIndex + offset) % partitions.Length])
                .ToArray();

            int remainingClaimCapacity = maximumCount - result.Count;
            var keys = new List<StackUsageKey>(Math.Min(remainingClaimCapacity, _pendingUsages.Count));
            for (int partitionIndex = 0; partitionIndex < partitions.Length && keys.Count < remainingClaimCapacity; partitionIndex++)
            {
                int remainingCapacity = remainingClaimCapacity - keys.Count;
                int remainingPartitions = partitions.Length - partitionIndex;
                int partitionQuota = Math.Max(1, remainingCapacity / remainingPartitions);
                keys.AddRange(partitions[partitionIndex].Take(partitionQuota));
            }

            foreach (var key in keys)
            {
                var usage = _pendingUsages[key];
                _pendingUsages.Remove(key);
                long settlementSequence = GetNextSettlementSequence(now);
                var inFlight = new InFlightStackUsage(
                    usage.MinimumOccurrenceDateUtc,
                    usage.MaximumOccurrenceDateUtc,
                    usage.Count,
                    settlementSequence,
                    leaseExpiresAt);
                _inFlightUsages[key] = inFlight;
                result.Add(inFlight.ToClaim(key));
            }

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AcknowledgeAsync(
        IReadOnlyCollection<StackUsageClaim> claims,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claims);
        if (claims.Count == 0)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            foreach (var claim in claims)
            {
                var key = new StackUsageKey(claim.OrganizationId, claim.ProjectId, claim.StackId);
                if (_inFlightUsages.TryGetValue(key, out var current)
                    && current.SettlementSequence == claim.SettlementSequence)
                {
                    _inFlightUsages.Remove(key);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private long GetNextSettlementSequence(DateTimeOffset now)
    {
        long clockSequence = checked(now.ToUnixTimeMilliseconds() * 1000L);
        _nextSettlementSequence = Math.Max(checked(_nextSettlementSequence + 1), clockSequence);
        return _nextSettlementSequence;
    }

    private void AddPendingUsage(IngestionStackUsage usage)
    {
        AddPendingUsage(new StackUsageSummary(
            usage.OrganizationId,
            usage.ProjectId,
            usage.StackId,
            usage.OccurrenceDateUtc,
            usage.OccurrenceDateUtc,
            1));
    }

    private void AddPendingUsage(StackUsageSummary usage)
    {
        var key = new StackUsageKey(usage.OrganizationId, usage.ProjectId, usage.StackId);
        if (_pendingUsages.TryGetValue(key, out var current))
        {
            current.Count += usage.Count;
            if (usage.MinimumOccurrenceDateUtc < current.MinimumOccurrenceDateUtc)
            {
                current.MinimumOccurrenceDateUtc = usage.MinimumOccurrenceDateUtc;
            }

            if (usage.MaximumOccurrenceDateUtc > current.MaximumOccurrenceDateUtc)
            {
                current.MaximumOccurrenceDateUtc = usage.MaximumOccurrenceDateUtc;
            }
        }
        else
        {
            _pendingUsages[key] = new MutableStackUsage(
                usage.MinimumOccurrenceDateUtc,
                usage.MaximumOccurrenceDateUtc,
                usage.Count);
        }
    }

    private void RemoveExpiredCompletions(DateTimeOffset now)
    {
        foreach (string eventId in _completedEvents
                     .Where(pair => pair.Value <= now)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _completedEvents.Remove(eventId);
        }
    }

    private sealed class MutableStackUsage(DateTime minimumOccurrenceDateUtc, DateTime maximumOccurrenceDateUtc, int count)
    {
        public DateTime MinimumOccurrenceDateUtc { get; set; } = minimumOccurrenceDateUtc;
        public DateTime MaximumOccurrenceDateUtc { get; set; } = maximumOccurrenceDateUtc;
        public int Count { get; set; } = count;

        public StackUsageSummary ToSummary(StackUsageKey key) => new(
            key.OrganizationId,
            key.ProjectId,
            key.StackId,
            MinimumOccurrenceDateUtc,
            MaximumOccurrenceDateUtc,
            Count);
    }

    private sealed class InFlightStackUsage(
        DateTime minimumOccurrenceDateUtc,
        DateTime maximumOccurrenceDateUtc,
        int count,
        long settlementSequence,
        DateTimeOffset leaseExpiresAt)
    {
        public DateTime MinimumOccurrenceDateUtc { get; } = minimumOccurrenceDateUtc;
        public DateTime MaximumOccurrenceDateUtc { get; } = maximumOccurrenceDateUtc;
        public int Count { get; } = count;
        public long SettlementSequence { get; } = settlementSequence;
        public DateTimeOffset LeaseExpiresAt { get; set; } = leaseExpiresAt;

        public StackUsageClaim ToClaim(StackUsageKey key) => new(
            key.OrganizationId,
            key.ProjectId,
            key.StackId,
            MinimumOccurrenceDateUtc,
            MaximumOccurrenceDateUtc,
            Count,
            SettlementSequence,
            SettlementSequence);
    }
}

public static class IngestionStackUsageStore
{
    public const int StatisticsStageFlag = 1 << 0;

    public static string GetStateKey(string projectId, string eventId) =>
        String.Concat("ingest-v3:{", projectId, "}:sideeffects:state:", eventId);

    public static string GetStateLockKey(string projectId, string eventId) =>
        String.Concat("ingest-v3:{", projectId, "}:sideeffects:lock:state:", eventId);

    internal static IReadOnlyList<IngestionStackUsage> Normalize(IReadOnlyCollection<IngestionStackUsage> usages)
    {
        ArgumentNullException.ThrowIfNull(usages);
        if (usages.Count == 0)
        {
            return [];
        }

        var unique = new Dictionary<(string ProjectId, string EventId), IngestionStackUsage>(usages.Count);
        foreach (var usage in usages)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(usage.EventId);
            ArgumentException.ThrowIfNullOrWhiteSpace(usage.OrganizationId);
            ArgumentException.ThrowIfNullOrWhiteSpace(usage.ProjectId);
            ArgumentException.ThrowIfNullOrWhiteSpace(usage.StackId);

            var normalized = usage with
            {
                OccurrenceDateUtc = usage.OccurrenceDateUtc.Kind switch
                {
                    DateTimeKind.Utc => usage.OccurrenceDateUtc,
                    DateTimeKind.Local => usage.OccurrenceDateUtc.ToUniversalTime(),
                    _ => DateTime.SpecifyKind(usage.OccurrenceDateUtc, DateTimeKind.Utc)
                }
            };
            var identity = (usage.ProjectId, usage.EventId);
            if (unique.TryGetValue(identity, out var existing) && existing != normalized)
            {
                throw new InvalidOperationException($"Event '{usage.EventId}' has conflicting stack-statistics data.");
            }

            unique[identity] = normalized;
        }

        return unique.Values
            .OrderBy(usage => usage.ProjectId, StringComparer.Ordinal)
            .ThenBy(usage => usage.EventId, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyCollection<StackUsageSummary> Summarize(IEnumerable<IngestionStackUsage> usages)
    {
        return usages
            .GroupBy(usage => new StackUsageKey(usage.OrganizationId, usage.ProjectId, usage.StackId))
            .Select(group => new StackUsageSummary(
                group.Key.OrganizationId,
                group.Key.ProjectId,
                group.Key.StackId,
                group.Min(usage => usage.OccurrenceDateUtc),
                group.Max(usage => usage.OccurrenceDateUtc),
                group.Count()))
            .OrderBy(usage => usage.StackId, StringComparer.Ordinal)
            .ToArray();
    }
}
