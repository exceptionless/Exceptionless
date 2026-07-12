using Foundatio.Caching;

namespace Exceptionless.Core.Services;

public interface IAtomicCacheBatch
{
    Task<bool> TrySetAllAsync(IReadOnlyDictionary<string, string?> expectedValues, IReadOnlyDictionary<string, string> values, TimeSpan expiresIn,
        IReadOnlyDictionary<string, string>? listValues = null, TimeSpan? listExpiresIn = null);
}

internal sealed class InMemoryAtomicCacheBatch(ICacheClient cacheClient) : IAtomicCacheBatch
{
    public async Task<bool> TrySetAllAsync(IReadOnlyDictionary<string, string?> expectedValues, IReadOnlyDictionary<string, string> values, TimeSpan expiresIn,
        IReadOnlyDictionary<string, string>? listValues = null, TimeSpan? listExpiresIn = null)
    {
        // The in-memory provider has no remote partial-failure boundary. Callers hold the
        // organization reservation lock while applying this batch.
        var currentValues = await cacheClient.GetAllAsync<string>(expectedValues.Keys);
        foreach (var expected in expectedValues)
        {
            string? current = currentValues.TryGetValue(expected.Key, out var value) && value.HasValue ? value.Value : null;
            if (!String.Equals(current, expected.Value, StringComparison.Ordinal))
                return false;
        }

        if (await cacheClient.SetAllAsync(values.ToDictionary(entry => entry.Key, entry => entry.Value), expiresIn) != values.Count)
            return false;

        if (listValues is not null)
        {
            foreach (var entry in listValues)
                await cacheClient.ListAddAsync(entry.Key, entry.Value, listExpiresIn);
        }

        return true;
    }
}
