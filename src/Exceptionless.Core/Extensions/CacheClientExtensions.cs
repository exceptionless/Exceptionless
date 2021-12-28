using Exceptionless.Core.Extensions;
using Foundatio.Caching;

namespace Exceptionless.Extensions;

public static class CacheClientExtensions {
    /// <summary>
    /// Increment a value if condition is true. Ensure any negative increments have a negative value.
    /// </summary>
    public static async Task<double> PositiveIncrementIfAsync(this ICacheClient client, string key, int value, TimeSpan timeToLive, bool shouldIncrement, long? startingValue = null) {
        startingValue ??= 0;

        var count = await client.GetAsync<long>(key).AnyContext();
        if (!shouldIncrement)
            return count.HasValue ? count.Value : startingValue.Value;

        if (count.HasValue) {
            var incrementedValue = await client.IncrementAsync(key, value, timeToLive).AnyContext();
            if (incrementedValue >= 0)
                return incrementedValue;

            return await client.IncrementAsync(key, Math.Abs(incrementedValue), timeToLive).AnyContext();
        }

        long newValue = Math.Max(0, startingValue.Value + value);
        await client.SetAsync(key, newValue, timeToLive).AnyContext();
        return newValue;
    }

    /// <summary>
    /// Increment a value if condition is true. Ensure any negative increments have a negative value.
    /// </summary>
    public static async Task<double> PositiveIncrementAsync(this ICacheClient client, string key, int value, TimeSpan timeToLive, long? startingValue = null) {
        startingValue ??= 0;

        var count = await client.GetAsync<long>(key).AnyContext();
        if (count.HasValue) {
            var incrementedValue = await client.IncrementAsync(key, value, timeToLive).AnyContext();
            if (incrementedValue >= 0)
                return incrementedValue;

            return await client.IncrementAsync(key, Math.Abs(incrementedValue), timeToLive).AnyContext();
        }

        long newValue = Math.Max(0, startingValue.Value + value);
        await client.SetAsync(key, newValue, timeToLive).AnyContext();
        return newValue;
    }
}
