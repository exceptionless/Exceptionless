using Exceptionless.Core.Configuration;
using Exceptionless.Core.Services;
using StackExchange.Redis;

namespace Exceptionless.Insulation.Redis;

public sealed class RedisAtomicCacheBatch(IConnectionMultiplexer connectionMultiplexer, CacheOptions cacheOptions, TimeProvider timeProvider) : IAtomicCacheBatch
{
    private const string CompareAndSetAllScript = """
        local ttl = ARGV[1]
        local valueCount = tonumber(ARGV[2])
        local listExpiresAt = ARGV[3]
        local missing = '__EX_MISSING__'
        for index = 1, valueCount do
            local current = redis.call('GET', KEYS[index])
            local expected = ARGV[index + 3]
            if expected == missing then
                if current then return 0 end
            elseif current ~= expected then
                return 0
            end
        end
        for index = 1, valueCount do
            redis.call('SET', KEYS[index], ARGV[valueCount + index + 3], 'PX', ttl)
        end
        for index = valueCount + 1, #KEYS do
            redis.call('ZADD', KEYS[index], listExpiresAt, ARGV[valueCount * 2 + index - valueCount + 3])
        end
        return 1
        """;

    public async Task<bool> TrySetAllAsync(IReadOnlyDictionary<string, string?> expectedValues, IReadOnlyDictionary<string, string> values, TimeSpan expiresIn,
        IReadOnlyDictionary<string, string>? listValues = null, TimeSpan? listExpiresIn = null)
    {
        if (values.Count == 0)
            return true;
        if (expectedValues.Count != values.Count || expectedValues.Keys.Any(key => !values.ContainsKey(key)))
            throw new ArgumentException("Expected and updated cache batches must contain the same keys.", nameof(expectedValues));

        string prefix = String.IsNullOrEmpty(cacheOptions.Scope) ? String.Empty : $"{cacheOptions.Scope}:";
        listValues ??= new Dictionary<string, string>();
        var keys = values.Keys.Concat(listValues.Keys).Select(key => (RedisKey)$"{prefix}{key}").ToArray();
        var arguments = new RedisValue[(values.Count * 2) + listValues.Count + 3];
        arguments[0] = checked((long)expiresIn.TotalMilliseconds);
        arguments[1] = values.Count;
        arguments[2] = timeProvider.GetUtcNow().Add(listExpiresIn ?? expiresIn).ToUnixTimeMilliseconds();
        int index = 3;
        foreach (string key in values.Keys)
            arguments[index++] = expectedValues[key] is { } expected ? expected : "__EX_MISSING__";
        foreach (string value in values.Values)
            arguments[index++] = value;
        foreach (string value in listValues.Values)
            arguments[index++] = value;

        try
        {
            var result = await connectionMultiplexer.GetDatabase().ScriptEvaluateAsync(CompareAndSetAllScript, keys, arguments);
            return (int)result == 1;
        }
        catch (RedisServerException ex) when (ex.Message.Contains("CROSSSLOT", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Atomic usage reservations require a single Redis endpoint; Redis Cluster hash-slot splitting is not supported.", ex);
        }
    }
}
