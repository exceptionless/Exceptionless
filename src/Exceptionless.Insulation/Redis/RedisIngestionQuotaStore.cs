using Exceptionless.Core.Services;
using StackExchange.Redis;

namespace Exceptionless.Insulation.Redis;

/// <summary>
/// Atomically tracks active ingestion capacity leases for one organization. Expired-lease cleanup
/// is bounded per call so a long-idle tenant cannot create an unbounded Redis script execution.
/// </summary>
public sealed class RedisIngestionQuotaStore(
    IConnectionMultiplexer connectionMultiplexer,
    TimeProvider timeProvider,
    string? scope) : IIngestionQuotaStore
{
    private const int CleanupLimit = 1000;
    private const string ReserveScript = """
        local active = tonumber(redis.call('GET', KEYS[3]) or '0')
        local expired = redis.call('ZRANGEBYSCORE', KEYS[2], '-inf', ARGV[1], 'LIMIT', 0, ARGV[7])
        for _, reservationId in ipairs(expired) do
            local count = tonumber(redis.call('HGET', KEYS[1], reservationId) or '0')
            active = active - count
            redis.call('HDEL', KEYS[1], reservationId)
            redis.call('ZREM', KEYS[2], reservationId)
        end

        if active < 0 then
            active = 0
        end

        if #expired > 0 then
            if redis.call('ZCARD', KEYS[2]) == 0 then
                redis.call('DEL', KEYS[1], KEYS[2], KEYS[3])
                active = 0
            else
                redis.call('SET', KEYS[3], active, 'KEEPTTL')
            end
        end

        local existing = redis.call('HGET', KEYS[1], ARGV[2])
        if existing then
            return tonumber(existing)
        end

        local remaining = tonumber(ARGV[4]) - active
        local admitted = tonumber(ARGV[3])
        if remaining < admitted then
            admitted = remaining
        end
        if admitted < 0 then
            admitted = 0
        end

        if admitted > 0 then
            redis.call('HSET', KEYS[1], ARGV[2], admitted)
            redis.call('ZADD', KEYS[2], ARGV[5], ARGV[2])
            active = active + admitted
            redis.call('SET', KEYS[3], active)
            redis.call('PEXPIRE', KEYS[1], ARGV[8])
            redis.call('PEXPIRE', KEYS[2], ARGV[8])
            redis.call('PEXPIRE', KEYS[3], ARGV[8])
        end

        return admitted
        """;

    private const string ReleaseScript = """
        local count = redis.call('HGET', KEYS[1], ARGV[1])
        if not count then
            return 0
        end

        count = tonumber(count)
        redis.call('HDEL', KEYS[1], ARGV[1])
        redis.call('ZREM', KEYS[2], ARGV[1])
        local active = tonumber(redis.call('GET', KEYS[3]) or '0') - count
        if active <= 0 or redis.call('ZCARD', KEYS[2]) == 0 then
            redis.call('DEL', KEYS[1], KEYS[2], KEYS[3])
        else
            redis.call('SET', KEYS[3], active, 'KEEPTTL')
        end

        return count
        """;

    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    private readonly string _scopePrefix = String.IsNullOrWhiteSpace(scope) ? String.Empty : String.Concat(scope, ":");

    public async Task<int> ReserveAsync(
        string organizationId,
        string reservationId,
        int requestedCount,
        int availableCount,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reservationId);
        ArgumentOutOfRangeException.ThrowIfNegative(requestedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(availableCount);
        if (expiresIn <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(expiresIn));
        if (requestedCount == 0)
            return 0;

        RedisKey[] keys = GetKeys(organizationId);
        long nowMilliseconds = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        long ttlMilliseconds = Math.Max(1, (long)Math.Ceiling(expiresIn.TotalMilliseconds));
        long stateTtlMilliseconds = checked(ttlMilliseconds * 2);
        RedisValue[] values =
        [
            nowMilliseconds,
            reservationId,
            requestedCount,
            availableCount,
            nowMilliseconds + ttlMilliseconds,
            ttlMilliseconds,
            CleanupLimit,
            stateTtlMilliseconds
        ];
        RedisResult result = await _database.ScriptEvaluateAsync(ReserveScript, keys, values);
        return checked((int)(long)result);
    }

    public Task ReleaseAsync(
        string organizationId,
        string reservationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reservationId);

        return _database.ScriptEvaluateAsync(
            ReleaseScript,
            GetKeys(organizationId),
            [reservationId]);
    }

    private RedisKey[] GetKeys(string organizationId) => GetKeys(_scopePrefix, organizationId);

    internal static RedisKey[] GetKeys(string scopePrefix, string organizationId)
    {
        // Outstanding work must survive bucket and finite plan-limit changes. Otherwise a caller
        // admitted just before a boundary can disappear from the active total and a second caller
        // can reuse the same monthly capacity. The organization hash tag keeps all atomic state in
        // one Redis Cluster slot.
        string keyPrefix = $"{scopePrefix}usage:reservations:v5:{{{organizationId}}}";
        return
        [
            String.Concat(keyPrefix, ":leases"),
            String.Concat(keyPrefix, ":expirations"),
            String.Concat(keyPrefix, ":active")
        ];
    }
}
