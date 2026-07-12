using Exceptionless.Core;
using Exceptionless.Core.Utility;
using StackExchange.Redis;

namespace Exceptionless.Insulation.Redis;

public sealed class RedisConnectionLeaseStore : IConnectionLeaseStore
{
    private const string AcquireScript = """
        local now = redis.call('TIME')
        local now_ms = (now[1] * 1000) + math.floor(now[2] / 1000)
        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', now_ms)
        if redis.call('ZSCORE', KEYS[1], ARGV[1]) then
          redis.call('ZADD', KEYS[1], now_ms + ARGV[3], ARGV[1])
          redis.call('PEXPIRE', KEYS[1], ARGV[3] * 2)
          return 1
        end
        if redis.call('ZCARD', KEYS[1]) >= tonumber(ARGV[2]) then return 0 end
        redis.call('ZADD', KEYS[1], now_ms + ARGV[3], ARGV[1])
        redis.call('PEXPIRE', KEYS[1], ARGV[3] * 2)
        return 1
        """;
    private const string RenewScript = """
        local now = redis.call('TIME')
        local now_ms = (now[1] * 1000) + math.floor(now[2] / 1000)
        local expires = redis.call('ZSCORE', KEYS[1], ARGV[1])
        if not expires or tonumber(expires) <= now_ms then
          redis.call('ZREM', KEYS[1], ARGV[1])
          return 0
        end
        redis.call('ZADD', KEYS[1], now_ms + ARGV[2], ARGV[1])
        redis.call('PEXPIRE', KEYS[1], ARGV[2] * 2)
        return 1
        """;
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly string _keyPrefix;

    public RedisConnectionLeaseStore(IConnectionMultiplexer multiplexer, AppOptions options)
    {
        _multiplexer = multiplexer;
        _keyPrefix = $"PushLease:{options.AppScope}:";
    }

    public async Task<bool> TryAcquireAsync(string userId, string connectionId, int maxConnections, TimeSpan leaseDuration)
    {
        try
        {
            var result = await Database.ScriptEvaluateAsync(AcquireScript, [GetKey(userId)], [connectionId, maxConnections, (long)leaseDuration.TotalMilliseconds]);
            return (long)result == 1;
        }
        catch (RedisException ex)
        {
            throw new ConnectionLeaseStoreException("Unable to acquire a push connection lease.", ex);
        }
    }

    public async Task<bool> RenewAsync(string userId, string connectionId, TimeSpan leaseDuration)
    {
        try
        {
            var result = await Database.ScriptEvaluateAsync(RenewScript, [GetKey(userId)], [connectionId, (long)leaseDuration.TotalMilliseconds]);
            return (long)result == 1;
        }
        catch (RedisException ex)
        {
            throw new ConnectionLeaseStoreException("Unable to renew a push connection lease.", ex);
        }
    }

    public async Task ReleaseAsync(string userId, string connectionId)
    {
        try
        {
            await Database.SortedSetRemoveAsync(GetKey(userId), connectionId);
        }
        catch (RedisException ex)
        {
            throw new ConnectionLeaseStoreException("Unable to release a push connection lease.", ex);
        }
    }

    private IDatabase Database => _multiplexer.GetDatabase();
    private RedisKey GetKey(string userId) => _keyPrefix + userId;
}
