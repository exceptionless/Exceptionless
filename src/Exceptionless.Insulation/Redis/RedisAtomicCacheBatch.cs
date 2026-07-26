using Exceptionless.Core.Configuration;
using Exceptionless.Core.Services;
using StackExchange.Redis;

namespace Exceptionless.Insulation.Redis;

public sealed class RedisAtomicCacheBatch : IAtomicCacheBatch
{
    private const string CompareAndSetAllScript = """
        local valueCount = tonumber(ARGV[1])
        local listExpiresAt = ARGV[2]
        for index = valueCount + 1, #KEYS do
            local keyType = redis.call('TYPE', KEYS[index])
            if type(keyType) == 'table' then keyType = keyType.ok end
            if keyType ~= 'none' and keyType ~= 'zset' then
                return redis.error_reply('Atomic cache list key has an incompatible Redis type')
            end
        end
        for index = 1, valueCount do
            local current = redis.call('GET', KEYS[index])
            local argumentIndex = 2 + ((index - 1) * 4)
            local expectedExists = ARGV[argumentIndex + 1]
            local expected = ARGV[argumentIndex + 2]
            if expectedExists == '0' then
                if current then return 0 end
            elseif current ~= expected then
                return 0
            end
        end
        for index = 1, valueCount do
            local argumentIndex = 2 + ((index - 1) * 4)
            redis.call('SET', KEYS[index], ARGV[argumentIndex + 3], 'PX', ARGV[argumentIndex + 4])
        end
        local listArgumentIndex = 2 + (valueCount * 4)
        for index = valueCount + 1, #KEYS do
            listArgumentIndex = listArgumentIndex + 1
            redis.call('ZADD', KEYS[index], listExpiresAt, ARGV[listArgumentIndex])
        end
        return 1
        """;

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly CacheOptions _cacheOptions;
    private readonly TimeProvider _timeProvider;

    public RedisAtomicCacheBatch(IConnectionMultiplexer connectionMultiplexer, CacheOptions cacheOptions, TimeProvider timeProvider)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _cacheOptions = cacheOptions;
        _timeProvider = timeProvider;

        if (connectionMultiplexer.GetServers().Any(server => server.ServerType == ServerType.Cluster))
            throw new NotSupportedException("Atomic usage reservations require a single Redis endpoint; Redis Cluster is not supported.");
    }

    public async Task<bool> TrySetAllAsync(IReadOnlyDictionary<string, string?> expectedValues, IReadOnlyDictionary<string, AtomicCacheValue> values,
        IReadOnlyDictionary<string, string>? listValues = null, TimeSpan? listExpiresIn = null)
    {
        AtomicCacheBatch.ValidateArguments(expectedValues, values, listValues, listExpiresIn);

        string prefix = String.IsNullOrEmpty(_cacheOptions.Scope) ? String.Empty : $"{_cacheOptions.Scope}:";
        listValues ??= new Dictionary<string, string>();
        var keys = values.Keys.Concat(listValues.Keys).Select(key => (RedisKey)$"{prefix}{key}").ToArray();
        var arguments = new RedisValue[(values.Count * 4) + listValues.Count + 2];
        arguments[0] = values.Count;
        arguments[1] = listValues.Count > 0
            ? _timeProvider.GetUtcNow().Add(listExpiresIn!.Value).ToUnixTimeMilliseconds()
            : 0;
        int index = 2;
        foreach (var entry in values)
        {
            string? expected = expectedValues[entry.Key];
            arguments[index++] = expected is null ? 0 : 1;
            arguments[index++] = expected ?? String.Empty;
            arguments[index++] = entry.Value.Value;
            arguments[index++] = checked((long)entry.Value.ExpiresIn.TotalMilliseconds);
        }
        foreach (string value in listValues.Values)
            arguments[index++] = value;

        try
        {
            var result = await _connectionMultiplexer.GetDatabase().ScriptEvaluateAsync(CompareAndSetAllScript, keys, arguments);
            return (int)result == 1;
        }
        catch (RedisServerException ex) when (ex.Message.Contains("CROSSSLOT", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Atomic usage reservations require a single Redis endpoint; Redis Cluster hash-slot splitting is not supported.", ex);
        }
    }
}
