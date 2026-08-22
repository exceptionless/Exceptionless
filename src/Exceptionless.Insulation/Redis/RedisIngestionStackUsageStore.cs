using System.Globalization;
using Exceptionless.Core;
using Exceptionless.Core.Services;
using StackExchange.Redis;

namespace Exceptionless.Insulation.Redis;

/// <summary>
/// Atomically deduplicates V3 statistics by event identity and drains stack aggregates through
/// leased, idempotent settlements. A settlement remains recoverable until Elasticsearch applies
/// it and the caller explicitly acknowledges it.
/// </summary>
public sealed class RedisIngestionStackUsageStore(
    IConnectionMultiplexer connectionMultiplexer,
    AppOptions options,
    string? scope) : IIngestionStackUsageStore
{
    private const int RegistryShardCount = 16;
    private const int MaximumProjectsPerClaim = 64;
    private const string SettleScript = """
        local stateTtl = tonumber(ARGV[1])
        local aggregateTtl = tonumber(ARGV[2])
        local eventCount = #KEYS - 10
        local accepted = {}
        local counts = {}
        local minimums = {}
        local maximums = {}
        local hasAccepted = false

        for index = 1, eventCount do
            local stateKey = KEYS[index + 10]
            local state = tonumber(redis.call('GET', stateKey) or '0')
            if bit.band(state, 1) == 0 then
                local argumentIndex = 3 + ((index - 1) * 2)
                local stackId = ARGV[argumentIndex]
                local occurrence = tonumber(ARGV[argumentIndex + 1])
                accepted[index] = 1
                hasAccepted = true
                counts[stackId] = (counts[stackId] or 0) + 1
                if not minimums[stackId] or occurrence < minimums[stackId] then
                    minimums[stackId] = occurrence
                end
                if not maximums[stackId] or occurrence > maximums[stackId] then
                    maximums[stackId] = occurrence
                end
                redis.call('SET', stateKey, bit.bor(state, 1), 'PX', stateTtl)
            else
                accepted[index] = 0
            end
        end

        if hasAccepted then
            for stackId, count in pairs(counts) do
                redis.call('HINCRBY', KEYS[2], stackId, count)
                if redis.call('HEXISTS', KEYS[6], stackId) == 0 then
                    redis.call('SADD', KEYS[1], stackId)
                end
                local currentMinimum = redis.call('HGET', KEYS[3], stackId)
                if not currentMinimum or minimums[stackId] < tonumber(currentMinimum) then
                    redis.call('HSET', KEYS[3], stackId, minimums[stackId])
                end
                local currentMaximum = redis.call('HGET', KEYS[4], stackId)
                if not currentMaximum or maximums[stackId] > tonumber(currentMaximum) then
                    redis.call('HSET', KEYS[4], stackId, maximums[stackId])
                end
            end
            for keyIndex = 1, 10 do
                redis.call('PEXPIRE', KEYS[keyIndex], aggregateTtl)
            end
        end

        return accepted
        """;

    private const string ClaimRegistryScript = """
        local currentTime = redis.call('TIME')
        local now = (tonumber(currentTime[1]) * 1000) + math.floor(tonumber(currentTime[2]) / 1000)
        local leaseUntil = now + tonumber(ARGV[2])
        local members = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', now, 'LIMIT', 0, tonumber(ARGV[1]))
        local result = {}
        for _, member in ipairs(members) do
            local leaseToken = redis.call('INCR', KEYS[4])
            redis.call('ZADD', KEYS[1], 'XX', leaseUntil, member)
            redis.call('HDEL', KEYS[2], member)
            redis.call('HSET', KEYS[3], member, leaseToken)
            result[#result + 1] = member
            result[#result + 1] = leaseToken
        end
        return result
        """;

    private const string ClaimPendingScript = """
        local maximumCount = tonumber(ARGV[1])
        local leaseDuration = tonumber(ARGV[2])
        local aggregateTtl = tonumber(ARGV[3])
        local currentTime = redis.call('TIME')
        local now = (tonumber(currentTime[1]) * 1000) + math.floor(tonumber(currentTime[2]) / 1000)
        local leaseUntil = now + leaseDuration
        local claims = {}
        local claimCount = 0

        local expired = redis.call('ZRANGEBYSCORE', KEYS[5], '-inf', now, 'LIMIT', 0, maximumCount)
        for _, stackId in ipairs(expired) do
            local sequence = redis.call('HGET', KEYS[6], stackId)
            local count = redis.call('HGET', KEYS[7], stackId)
            local minimum = redis.call('HGET', KEYS[8], stackId)
            local maximum = redis.call('HGET', KEYS[9], stackId)
            if sequence and count and minimum and maximum then
                redis.call('ZADD', KEYS[5], leaseUntil, stackId)
                claims[#claims + 1] = stackId
                claims[#claims + 1] = sequence
                claims[#claims + 1] = count
                claims[#claims + 1] = minimum
                claims[#claims + 1] = maximum
                claimCount = claimCount + 1
            else
                redis.call('ZREM', KEYS[5], stackId)
                redis.call('HDEL', KEYS[6], stackId)
                redis.call('HDEL', KEYS[7], stackId)
                redis.call('HDEL', KEYS[8], stackId)
                redis.call('HDEL', KEYS[9], stackId)
                if redis.call('HEXISTS', KEYS[2], stackId) == 1 then
                    redis.call('SADD', KEYS[1], stackId)
                end
            end
        end

        local remaining = maximumCount - claimCount
        if remaining > 0 then
            local pending = redis.call('SPOP', KEYS[1], remaining)
            for _, stackId in ipairs(pending) do
                local count = redis.call('HGET', KEYS[2], stackId)
                local minimum = redis.call('HGET', KEYS[3], stackId)
                local maximum = redis.call('HGET', KEYS[4], stackId)
                if count and minimum and maximum and redis.call('HEXISTS', KEYS[6], stackId) == 0 then
                    local clockSequence = now * 1000
                    local currentSequence = tonumber(redis.call('GET', KEYS[10]) or '0')
                    local sequence = currentSequence + 1
                    if clockSequence > sequence then
                        sequence = clockSequence
                    end
                    local sequenceValue = string.format('%.0f', sequence)
                    redis.call('SET', KEYS[10], sequenceValue)
                    redis.call('HSET', KEYS[6], stackId, sequenceValue)
                    redis.call('HSET', KEYS[7], stackId, count)
                    redis.call('HSET', KEYS[8], stackId, minimum)
                    redis.call('HSET', KEYS[9], stackId, maximum)
                    redis.call('HDEL', KEYS[2], stackId)
                    redis.call('HDEL', KEYS[3], stackId)
                    redis.call('HDEL', KEYS[4], stackId)
                    redis.call('ZADD', KEYS[5], leaseUntil, stackId)
                    claims[#claims + 1] = stackId
                    claims[#claims + 1] = sequenceValue
                    claims[#claims + 1] = count
                    claims[#claims + 1] = minimum
                    claims[#claims + 1] = maximum
                    claimCount = claimCount + 1
                elseif count and minimum and maximum then
                    redis.call('SADD', KEYS[1], stackId)
                else
                    redis.call('HDEL', KEYS[2], stackId)
                    redis.call('HDEL', KEYS[3], stackId)
                    redis.call('HDEL', KEYS[4], stackId)
                end
            end
        end

        local pendingCount = redis.call('SCARD', KEYS[1])
        local inFlightCount = redis.call('ZCARD', KEYS[5])
        local nextDue = -1
        if pendingCount > 0 then
            nextDue = now
        elseif inFlightCount > 0 then
            local nextClaim = redis.call('ZRANGE', KEYS[5], 0, 0, 'WITHSCORES')
            if #nextClaim == 2 then
                nextDue = tonumber(nextClaim[2])
            end
        end
        if pendingCount > 0 or inFlightCount > 0 then
            for keyIndex = 1, 10 do
                redis.call('PEXPIRE', KEYS[keyIndex], aggregateTtl)
            end
        end

        local result = { pendingCount, inFlightCount, nextDue, now }
        for _, value in ipairs(claims) do
            result[#result + 1] = value
        end
        return result
        """;

    private const string AcknowledgeScript = """
        local aggregateTtl = tonumber(ARGV[1])
        local claimCount = (#ARGV - 1) / 2
        for index = 1, claimCount do
            local argumentIndex = 2 + ((index - 1) * 2)
            local stackId = ARGV[argumentIndex]
            local sequence = ARGV[argumentIndex + 1]
            local currentSequence = redis.call('HGET', KEYS[6], stackId)
            if currentSequence and currentSequence == sequence then
                redis.call('ZREM', KEYS[5], stackId)
                redis.call('HDEL', KEYS[6], stackId)
                redis.call('HDEL', KEYS[7], stackId)
                redis.call('HDEL', KEYS[8], stackId)
                redis.call('HDEL', KEYS[9], stackId)
                if redis.call('HEXISTS', KEYS[2], stackId) == 1 then
                    redis.call('SADD', KEYS[1], stackId)
                end
            end
        end

        local currentTime = redis.call('TIME')
        local now = (tonumber(currentTime[1]) * 1000) + math.floor(tonumber(currentTime[2]) / 1000)
        local pendingCount = redis.call('SCARD', KEYS[1])
        local inFlightCount = redis.call('ZCARD', KEYS[5])
        local nextDue = -1
        if pendingCount > 0 then
            nextDue = now
        elseif inFlightCount > 0 then
            local nextClaim = redis.call('ZRANGE', KEYS[5], 0, 0, 'WITHSCORES')
            if #nextClaim == 2 then
                nextDue = tonumber(nextClaim[2])
            end
        end
        if pendingCount > 0 or inFlightCount > 0 then
            for keyIndex = 1, 10 do
                redis.call('PEXPIRE', KEYS[keyIndex], aggregateTtl)
            end
        end
        return { pendingCount, inFlightCount, nextDue, now }
        """;

    private const string ReserveRegistryScript = """
        local currentTime = redis.call('TIME')
        local now = (tonumber(currentTime[1]) * 1000) + math.floor(tonumber(currentTime[2]) / 1000)
        local current = redis.call('ZSCORE', KEYS[1], ARGV[1])
        local reservation = redis.call('HGET', KEYS[2], ARGV[1])
        if reservation then
            return { reservation, current or reservation }
        end
        if current then
            return { '', current }
        end
        local reservationUntil = now + tonumber(ARGV[2])
        local token = string.format('%.0f', reservationUntil)
        redis.call('ZADD', KEYS[1], reservationUntil, ARGV[1])
        redis.call('HSET', KEYS[2], ARGV[1], token)
        return { token, token }
        """;

    private const string ActivateRegistryScript = """
        local member = ARGV[1]
        local token = ARGV[2]
        local reservation = redis.call('HGET', KEYS[2], member)
        local currentTime = redis.call('TIME')
        local now = (tonumber(currentTime[1]) * 1000) + math.floor(tonumber(currentTime[2]) / 1000)
        if token ~= '' and reservation and reservation == token then
            redis.call('HDEL', KEYS[2], member)
        end
        redis.call('HDEL', KEYS[3], member)
        redis.call('ZADD', KEYS[1], now, member)
        return 1
        """;

    private const string FinalizeRegistryScript = """
        local currentLease = redis.call('HGET', KEYS[3], ARGV[1])
        if not currentLease or currentLease ~= ARGV[2] then
            return 0
        end
        redis.call('HDEL', KEYS[3], ARGV[1])
        local nextDue = tonumber(ARGV[3])
        if nextDue < 0 then
            redis.call('HDEL', KEYS[2], ARGV[1])
            return redis.call('ZREM', KEYS[1], ARGV[1])
        end
        redis.call('ZADD', KEYS[1], nextDue, ARGV[1])
        return 1
        """;

    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    private readonly string _scopePrefix = String.IsNullOrWhiteSpace(scope) ? String.Empty : String.Concat(scope.Trim(), ":");
    private readonly TimeSpan _claimLease = NormalizeClaimLease(options.EventIngestionV3.StackUsageClaimLease);
    private readonly TimeSpan _aggregateExpiration = GetAggregateExpiration(options.EventIngestionV3);
    private int _registryCursor = -1;

    public async Task<IReadOnlyCollection<StackUsageSummary>> SettleAsync(
        IReadOnlyCollection<IngestionStackUsage> usages,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = IngestionStackUsageStore.Normalize(usages);
        if (normalized.Count == 0)
        {
            return [];
        }

        var first = normalized[0];
        if (normalized.Any(usage => !String.Equals(usage.ProjectId, first.ProjectId, StringComparison.Ordinal)
            || !String.Equals(usage.OrganizationId, first.OrganizationId, StringComparison.Ordinal)))
        {
            throw new ArgumentException("An ingestion stack-statistics settlement must contain one project.", nameof(usages));
        }

        RegistryReservation reservation = await ReserveRegistryAsync(first.OrganizationId, first.ProjectId);
        RedisKey[] keys = GetKeys(_scopePrefix, first.ProjectId, normalized.Select(usage => usage.EventId));
        var values = new RedisValue[2 + (normalized.Count * 2)];
        values[0] = GetExpirationMilliseconds(options.EventIngestionV3.IdempotencyWindow);
        values[1] = GetExpirationMilliseconds(_aggregateExpiration);
        for (int index = 0; index < normalized.Count; index++)
        {
            values[2 + (index * 2)] = normalized[index].StackId;
            values[3 + (index * 2)] = ToUnixTimeMilliseconds(normalized[index].OccurrenceDateUtc);
        }

        RedisResult result = await _database.ScriptEvaluateAsync(SettleScript, keys, values);
        var resultValues = (RedisResult[]?)result ?? [];
        if (resultValues.Length != normalized.Count)
        {
            throw new InvalidOperationException("Redis returned an invalid ingestion stack-statistics settlement result.");
        }

        var newlySettled = new List<IngestionStackUsage>(normalized.Count);
        for (int index = 0; index < normalized.Count; index++)
        {
            if ((long)resultValues[index] == 1)
            {
                newlySettled.Add(normalized[index]);
            }
        }
        // Always activate after the atomic settlement, including an idempotent retry that
        // accepted no new events. A previous attempt may have committed the aggregate and
        // stopped before activation while an older worker still owns a stale registry lease.
        await ActivateRegistryAsync(reservation);
        return IngestionStackUsageStore.Summarize(newlySettled);
    }

    public async Task<IReadOnlyCollection<StackUsageClaim>> ClaimPendingAsync(
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);

        IReadOnlyList<RegistryPartition> partitions = await ClaimRegistryPartitionsAsync(
            Math.Min(maximumCount, MaximumProjectsPerClaim),
            cancellationToken);
        if (partitions.Count == 0)
        {
            return [];
        }

        var claims = new List<StackUsageClaim>(Math.Min(maximumCount, partitions.Count));
        for (int partitionIndex = 0; partitionIndex < partitions.Count && claims.Count < maximumCount; partitionIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var partition = partitions[partitionIndex];
            int remainingCapacity = maximumCount - claims.Count;
            int remainingPartitions = partitions.Count - partitionIndex;
            int partitionQuota = Math.Max(1, remainingCapacity / remainingPartitions);
            RedisResult result = await _database.ScriptEvaluateAsync(
                ClaimPendingScript,
                GetAggregateKeys(_scopePrefix, partition.ProjectId),
                [partitionQuota, GetExpirationMilliseconds(_claimLease), GetExpirationMilliseconds(_aggregateExpiration)]);
            var values = (RedisResult[]?)result ?? [];
            AggregateState state = ParseAggregateState(values, 4, "claim");
            if ((values.Length - 4) % 5 != 0)
            {
                throw new InvalidOperationException("Redis returned an invalid pending stack-statistics claim result.");
            }

            for (int index = 4; index < values.Length; index += 5)
            {
                claims.Add(new StackUsageClaim(
                    partition.OrganizationId,
                    partition.ProjectId,
                    (string)values[index]!,
                    FromUnixTimeMilliseconds((long)values[index + 3]),
                    FromUnixTimeMilliseconds((long)values[index + 4]),
                    checked((int)(long)values[index + 2]),
                    (long)values[index + 1],
                    partition.LeaseToken));
            }

            if (values.Length == 4)
            {
                await FinalizeRegistryAsync(partition, state);
            }
        }

        return claims;
    }

    public async Task AcknowledgeAsync(
        IReadOnlyCollection<StackUsageClaim> claims,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(claims);
        if (claims.Count == 0)
        {
            return;
        }

        foreach (var group in claims.GroupBy(claim => (claim.OrganizationId, claim.ProjectId, claim.LeaseToken)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            StackUsageClaim[] projectClaims = group.ToArray();
            var values = new RedisValue[1 + (projectClaims.Length * 2)];
            values[0] = GetExpirationMilliseconds(_aggregateExpiration);
            for (int index = 0; index < projectClaims.Length; index++)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(projectClaims[index].SettlementSequence);
                values[1 + (index * 2)] = projectClaims[index].StackId;
                values[2 + (index * 2)] = projectClaims[index].SettlementSequence;
            }

            RedisResult result = await _database.ScriptEvaluateAsync(
                AcknowledgeScript,
                GetAggregateKeys(_scopePrefix, group.Key.ProjectId),
                values);
            AggregateState state = ParseAggregateState((RedisResult[]?)result ?? [], 4, "acknowledgement");
            await FinalizeRegistryAsync(
                new RegistryPartition(
                    GetRegistryKey(_scopePrefix, group.Key.ProjectId),
                    GetRegistryReservationKey(_scopePrefix, group.Key.ProjectId),
                    GetRegistryLeaseKey(_scopePrefix, group.Key.ProjectId),
                    EncodeRegistryField(group.Key.OrganizationId, group.Key.ProjectId),
                    group.Key.OrganizationId,
                    group.Key.ProjectId,
                    group.Key.LeaseToken),
                state);
        }
    }

    internal static RedisKey[] GetKeys(string scopePrefix, string projectId, IEnumerable<string> eventIds)
    {
        return GetAggregateKeys(scopePrefix, projectId)
            .Concat(eventIds.Select(eventId => (RedisKey)String.Concat(scopePrefix, IngestionStackUsageStore.GetStateKey(projectId, eventId))))
            .ToArray();
    }

    internal static RedisKey[] GetAggregateKeys(string scopePrefix, string projectId)
    {
        string prefix = String.Concat(scopePrefix, "ingest-v3:{", projectId, "}:stack-usage:");
        return
        [
            String.Concat(prefix, "pending"),
            String.Concat(prefix, "counts"),
            String.Concat(prefix, "minimums"),
            String.Concat(prefix, "maximums"),
            String.Concat(prefix, "inflight-expirations"),
            String.Concat(prefix, "inflight-sequences"),
            String.Concat(prefix, "inflight-counts"),
            String.Concat(prefix, "inflight-minimums"),
            String.Concat(prefix, "inflight-maximums"),
            String.Concat(prefix, "settlement-sequence")
        ];
    }

    internal static RedisKey[] GetRegistryKeys(string scopePrefix) =>
        Enumerable.Range(0, RegistryShardCount)
            .Select(shard => (RedisKey)$"{scopePrefix}ingest-v3:{{stack-usage-registry-{shard:D2}}}:projects")
            .ToArray();

    internal static RedisKey[] GetRegistryReservationKeys(string scopePrefix) =>
        GetRegistryKeys(scopePrefix)
            .Select(key => (RedisKey)String.Concat(key.ToString(), ":reservations"))
            .ToArray();

    internal static RedisKey[] GetRegistryLeaseKeys(string scopePrefix) =>
        GetRegistryKeys(scopePrefix)
            .Select(key => (RedisKey)String.Concat(key.ToString(), ":leases"))
            .ToArray();

    internal static RedisKey[] GetRegistryCounterKeys(string scopePrefix) =>
        GetRegistryKeys(scopePrefix)
            .Select(key => (RedisKey)String.Concat(key.ToString(), ":counter"))
            .ToArray();

    private async Task<IReadOnlyList<RegistryPartition>> ClaimRegistryPartitionsAsync(
        int maximumCount,
        CancellationToken cancellationToken)
    {
        RedisKey[] registryKeys = GetRegistryKeys(_scopePrefix);
        RedisKey[] reservationKeys = GetRegistryReservationKeys(_scopePrefix);
        RedisKey[] leaseKeys = GetRegistryLeaseKeys(_scopePrefix);
        RedisKey[] counterKeys = GetRegistryCounterKeys(_scopePrefix);
        int startShard = (int)((uint)Interlocked.Increment(ref _registryCursor) % RegistryShardCount);
        var partitions = new List<RegistryPartition>(maximumCount);
        for (int offset = 0; offset < RegistryShardCount && partitions.Count < maximumCount; offset++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int shard = (startShard + offset) % RegistryShardCount;
            RedisKey registryKey = registryKeys[shard];
            RedisResult result = await _database.ScriptEvaluateAsync(
                ClaimRegistryScript,
                [registryKey, reservationKeys[shard], leaseKeys[shard], counterKeys[shard]],
                [maximumCount - partitions.Count, GetExpirationMilliseconds(_claimLease)]);
            var values = (RedisResult[]?)result ?? [];
            if (values.Length % 2 != 0)
            {
                throw new InvalidOperationException("Redis returned an invalid stack-statistics registry claim result.");
            }

            for (int index = 0; index < values.Length; index += 2)
            {
                partitions.Add(DecodeRegistryMember(registryKey, (string)values[index]!, (long)values[index + 1]));
            }
        }
        return partitions;
    }

    private async Task<RegistryReservation> ReserveRegistryAsync(string organizationId, string projectId)
    {
        RedisKey registryKey = GetRegistryKey(_scopePrefix, projectId);
        RedisKey reservationKey = GetRegistryReservationKey(_scopePrefix, projectId);
        string member = EncodeRegistryField(organizationId, projectId);
        RedisResult result = await _database.ScriptEvaluateAsync(
            ReserveRegistryScript,
            [registryKey, reservationKey],
            [member, GetExpirationMilliseconds(_claimLease)]);
        var values = (RedisResult[]?)result ?? [];
        if (values.Length != 2)
        {
            throw new InvalidOperationException("Redis returned an invalid stack-statistics registry reservation result.");
        }

        return new RegistryReservation(registryKey, reservationKey, member, (string?)values[0] ?? String.Empty);
    }

    private Task ActivateRegistryAsync(RegistryReservation reservation)
    {
        return _database.ScriptEvaluateAsync(
            ActivateRegistryScript,
            [reservation.RegistryKey, reservation.ReservationKey, GetRegistryLeaseKey(reservation.RegistryKey)],
            [reservation.RegistryMember, reservation.Token]);
    }

    private Task FinalizeRegistryAsync(RegistryPartition partition, AggregateState state)
    {
        long nextDue = state.PendingCount > 0 ? state.Now : state.NextDue;
        return _database.ScriptEvaluateAsync(
            FinalizeRegistryScript,
            [partition.RegistryKey, partition.ReservationKey, partition.LeaseKey],
            [partition.RegistryMember, partition.LeaseToken, nextDue]);
    }

    private static AggregateState ParseAggregateState(RedisResult[] values, int minimumLength, string operation)
    {
        if (values.Length < minimumLength)
        {
            throw new InvalidOperationException($"Redis returned an invalid stack-statistics {operation} result.");
        }

        return new AggregateState(
            (long)values[0],
            (long)values[1],
            (long)values[2],
            (long)values[3]);
    }

    private static RedisKey GetRegistryKey(string scopePrefix, string projectId) =>
        GetRegistryKeys(scopePrefix)[GetRegistryShard(projectId)];

    private static RedisKey GetRegistryReservationKey(string scopePrefix, string projectId) =>
        GetRegistryReservationKeys(scopePrefix)[GetRegistryShard(projectId)];

    private static RedisKey GetRegistryLeaseKey(string scopePrefix, string projectId) =>
        GetRegistryLeaseKeys(scopePrefix)[GetRegistryShard(projectId)];

    private static RedisKey GetRegistryLeaseKey(RedisKey registryKey) =>
        String.Concat(registryKey.ToString(), ":leases");

    private static int GetRegistryShard(string projectId)
    {
        uint hash = 2166136261;
        foreach (char character in projectId)
        {
            hash = (hash ^ character) * 16777619;
        }

        return (int)(hash % RegistryShardCount);
    }

    private static string EncodeRegistryField(string organizationId, string projectId) =>
        String.Concat(
            organizationId.Length.ToString(CultureInfo.InvariantCulture), ":", organizationId,
            projectId.Length.ToString(CultureInfo.InvariantCulture), ":", projectId);

    private static RegistryPartition DecodeRegistryMember(RedisKey registryKey, string value, long leaseToken)
    {
        int offset = 0;
        string organizationId = ReadPart(value, ref offset);
        string projectId = ReadPart(value, ref offset);
        if (offset != value.Length)
        {
            throw new InvalidOperationException("Redis returned an invalid stack-statistics registry member.");
        }

        return new RegistryPartition(
            registryKey,
            String.Concat(registryKey.ToString(), ":reservations"),
            GetRegistryLeaseKey(registryKey),
            value,
            organizationId,
            projectId,
            leaseToken);
    }

    private static string ReadPart(string value, ref int offset)
    {
        int separator = value.IndexOf(':', offset);
        if (separator < 0
            || !Int32.TryParse(value.AsSpan(offset, separator - offset), NumberStyles.None, CultureInfo.InvariantCulture, out int length)
            || length < 1
            || separator + 1 + length > value.Length)
        {
            throw new InvalidOperationException("Redis returned an invalid stack-statistics registry member.");
        }
        offset = separator + 1;
        string part = value.Substring(offset, length);
        offset += length;
        return part;
    }

    private static TimeSpan NormalizeClaimLease(TimeSpan value) => value > TimeSpan.Zero ? value : TimeSpan.FromMinutes(1);

    private static TimeSpan GetAggregateExpiration(EventIngestionV3Options ingestionOptions)
    {
        TimeSpan idempotencyWindow = ingestionOptions.IdempotencyWindow > TimeSpan.Zero
            ? ingestionOptions.IdempotencyWindow
            : TimeSpan.FromDays(7);
        TimeSpan claimLease = NormalizeClaimLease(ingestionOptions.StackUsageClaimLease);
        long safetyTicks = checked(claimLease.Ticks + TimeSpan.FromDays(1).Ticks);
        if (idempotencyWindow.Ticks > TimeSpan.MaxValue.Ticks - safetyTicks)
        {
            return TimeSpan.MaxValue;
        }

        return idempotencyWindow.Add(TimeSpan.FromTicks(safetyTicks));
    }

    private static long GetExpirationMilliseconds(TimeSpan value) => Math.Max(1, checked((long)Math.Ceiling(value.TotalMilliseconds)));

    private static long ToUnixTimeMilliseconds(DateTime value)
    {
        DateTime utcValue = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        return new DateTimeOffset(utcValue).ToUnixTimeMilliseconds();
    }

    private static DateTime FromUnixTimeMilliseconds(long value) => DateTimeOffset.FromUnixTimeMilliseconds(value).UtcDateTime;

    private sealed record RegistryPartition(
        RedisKey RegistryKey,
        RedisKey ReservationKey,
        RedisKey LeaseKey,
        string RegistryMember,
        string OrganizationId,
        string ProjectId,
        long LeaseToken);

    private sealed record RegistryReservation(
        RedisKey RegistryKey,
        RedisKey ReservationKey,
        string RegistryMember,
        string Token);

    private readonly record struct AggregateState(long PendingCount, long InFlightCount, long NextDue, long Now);
}
