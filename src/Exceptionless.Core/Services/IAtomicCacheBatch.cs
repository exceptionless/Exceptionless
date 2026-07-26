using Foundatio.Caching;

namespace Exceptionless.Core.Services;

public readonly record struct AtomicCacheValue(string Value, TimeSpan ExpiresIn);

public interface IAtomicCacheBatch
{
    Task<bool> TrySetAllAsync(IReadOnlyDictionary<string, string?> expectedValues, IReadOnlyDictionary<string, AtomicCacheValue> values,
        IReadOnlyDictionary<string, string>? listValues = null, TimeSpan? listExpiresIn = null);
}

internal sealed class InMemoryAtomicCacheBatch(ICacheClient cacheClient) : IAtomicCacheBatch
{
    public async Task<bool> TrySetAllAsync(IReadOnlyDictionary<string, string?> expectedValues, IReadOnlyDictionary<string, AtomicCacheValue> values,
        IReadOnlyDictionary<string, string>? listValues = null, TimeSpan? listExpiresIn = null)
    {
        AtomicCacheBatch.ValidateArguments(expectedValues, values, listValues, listExpiresIn);

        // The in-memory provider has no remote partial-failure boundary. Callers hold the
        // organization reservation lock while applying this batch.
        var currentValues = await cacheClient.GetAllAsync<string>(expectedValues.Keys);
        foreach (var expected in expectedValues)
        {
            string? current = currentValues.TryGetValue(expected.Key, out var value) && value.HasValue ? value.Value : null;
            if (!String.Equals(current, expected.Value, StringComparison.Ordinal))
                return false;
        }

        foreach (var expirationGroup in values.GroupBy(entry => entry.Value.ExpiresIn))
        {
            var updates = expirationGroup.ToDictionary(entry => entry.Key, entry => entry.Value.Value);
            if (await cacheClient.SetAllAsync(updates, expirationGroup.Key) != updates.Count)
                return false;
        }

        if (listValues is not null)
        {
            foreach (var entry in listValues)
                await cacheClient.ListAddAsync(entry.Key, entry.Value, listExpiresIn);
        }

        return true;
    }
}

internal static class AtomicCacheBatch
{
    public static void ValidateArguments(IReadOnlyDictionary<string, string?> expectedValues, IReadOnlyDictionary<string, AtomicCacheValue> values,
        IReadOnlyDictionary<string, string>? listValues, TimeSpan? listExpiresIn)
    {
        ArgumentNullException.ThrowIfNull(expectedValues);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new ArgumentException("Atomic cache batches must contain at least one scalar update.", nameof(values));
        if (expectedValues.Count != values.Count || expectedValues.Keys.Any(key => !values.ContainsKey(key)))
            throw new ArgumentException("Expected and updated cache batches must contain the same keys.", nameof(expectedValues));
        if (values.Any(entry => entry.Value.ExpiresIn < TimeSpan.FromMilliseconds(1)))
            throw new ArgumentOutOfRangeException(nameof(values), "Atomic cache update expirations must be at least one millisecond.");

        if (listValues is not { Count: > 0 })
            return;
        if (listExpiresIn is null || listExpiresIn < TimeSpan.FromMilliseconds(1))
            throw new ArgumentOutOfRangeException(nameof(listExpiresIn), "Atomic cache list expirations must be at least one millisecond.");
        if (listValues.Keys.Any(values.ContainsKey))
            throw new ArgumentException("Scalar and list cache updates must use different keys.", nameof(listValues));
    }
}
