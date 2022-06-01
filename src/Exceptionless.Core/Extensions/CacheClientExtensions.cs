using Exceptionless.Core.Extensions;
using Foundatio.Caching;

namespace Exceptionless.Extensions;

public static class CacheClientExtensions {
    /// <summary>
    /// Increment a value if condition is true.
    /// </summary>
    public static async Task<long> IncrementIfAsync(this ICacheClient client, string key, int value, TimeSpan timeToLive, bool shouldIncrement, long? startingValue = null) {
        startingValue ??= 0;

        var count = await client.GetAsync<long>(key).AnyContext();
        if (!shouldIncrement)
            return count.HasValue ? count.Value : startingValue.Value;

        if (count.HasValue)
            return await client.IncrementAsync(key, value, timeToLive).AnyContext();

        long newValue = startingValue.Value + value;
        await client.SetAsync(key, newValue, timeToLive).AnyContext();
        return newValue;
    }

    /// <summary>
    /// Increment a value if condition is true.
    /// </summary>
    public static async Task<long> IncrementAsync(this ICacheClient client, string key, int value, TimeSpan timeToLive, long? startingValue = null) {
        startingValue ??= 0;

        var count = await client.GetAsync<long>(key).AnyContext();
        if (count.HasValue)
            return await client.IncrementAsync(key, value, timeToLive).AnyContext();

        long newValue = startingValue.Value + value;
        await client.SetAsync(key, newValue, timeToLive).AnyContext();
        return newValue;
    }
}
