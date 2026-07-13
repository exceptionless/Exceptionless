using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;

namespace Exceptionless.Core.Services;

public interface IStackRouteResolver
{
    Task<IReadOnlyDictionary<string, StackRoute>> ResolveAsync(string projectId, IReadOnlyCollection<string> signatureHashes, CancellationToken cancellationToken = default);
    Task UpdateAsync(string projectId, string signatureHash, StackRoute route);
    Task RemoveAsync(string projectId, string signatureHash);
}

public sealed class StackRouteResolver(IStackRepository stackRepository, ICacheClient cache, AppOptions options) : IStackRouteResolver
{
    public async Task<IReadOnlyDictionary<string, StackRoute>> ResolveAsync(string projectId, IReadOnlyCollection<string> signatureHashes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var activity = AppDiagnostics.StartActivity("Ingestion V3 Stack Route Resolve");
        using var timer = AppDiagnostics.IngestionV3RouteResolutionTime.StartTimer();
        string[] distinctHashes = signatureHashes.Distinct(StringComparer.Ordinal).ToArray();
        AppDiagnostics.IngestionV3RouteLocalDeduplicated.Add(signatureHashes.Count - distinctHashes.Length);
        if (distinctHashes.Length == 0)
            return new Dictionary<string, StackRoute>();

        var cacheKeys = distinctHashes.Select(hash => GetCacheKey(projectId, hash)).ToArray();
        var cached = await cache.GetAllAsync<StackRouteCacheEntry>(cacheKeys);
        cancellationToken.ThrowIfCancellationRequested();
        var routes = new Dictionary<string, StackRoute>(distinctHashes.Length, StringComparer.Ordinal);
        var misses = new List<string>();

        for (int i = 0; i < distinctHashes.Length; i++)
        {
            string hash = distinctHashes[i];
            string key = cacheKeys[i];
            if (!cached.TryGetValue(key, out var cacheValue) || !cacheValue.HasValue)
            {
                AppDiagnostics.IngestionV3RouteCacheMisses.Add(1);
                misses.Add(hash);
                continue;
            }

            StackRoute? route = cacheValue.Value.ToRoute();
            if (route is not null)
            {
                AppDiagnostics.IngestionV3RouteCacheHits.Add(1);
                routes[hash] = route;
            }
            else
            {
                AppDiagnostics.IngestionV3RouteNegativeHits.Add(1);
            }
        }

        if (misses.Count == 0)
            return routes;

        AppDiagnostics.IngestionV3RouteRepositoryLookups.Add(misses.Count);
        var loaded = await stackRepository.GetStackRoutesBySignatureHashAsync(projectId, misses);
        cancellationToken.ThrowIfCancellationRequested();
        var positiveEntries = new Dictionary<string, StackRouteCacheEntry>(misses.Count);
        var negativeEntries = new Dictionary<string, StackRouteCacheEntry>(misses.Count);
        foreach (string hash in misses)
        {
            if (loaded.TryGetValue(hash, out var route))
            {
                routes[hash] = route;
                positiveEntries[GetCacheKey(projectId, hash)] = new StackRouteCacheEntry(true, route.StackId, route.Status);
            }
            else
            {
                negativeEntries[GetCacheKey(projectId, hash)] = new StackRouteCacheEntry(false);
            }
        }

        if (positiveEntries.Count > 0)
            await cache.SetAllAsync(positiveEntries, options.EventIngestionV3.StackRouteCacheDuration);
        if (negativeEntries.Count > 0)
            await cache.SetAllAsync(negativeEntries, options.EventIngestionV3.NegativeStackRouteCacheDuration);
        return routes;
    }

    public Task UpdateAsync(string projectId, string signatureHash, StackRoute route)
    {
        return cache.SetAsync(GetCacheKey(projectId, signatureHash), new StackRouteCacheEntry(true, route.StackId, route.Status), options.EventIngestionV3.StackRouteCacheDuration);
    }

    public Task RemoveAsync(string projectId, string signatureHash)
    {
        return cache.RemoveAsync(GetCacheKey(projectId, signatureHash));
    }

    internal static string GetCacheKey(string projectId, string signatureHash) => String.Concat("stack-route:v3:v1:", projectId, ":", signatureHash);
}
