using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Repositories;

namespace Exceptionless.Core.Services;

public interface IStackRouteResolver
{
    Task<IReadOnlyDictionary<string, StackRoute>> ResolveAsync(string projectId, IReadOnlyCollection<string> signatureHashes, CancellationToken cancellationToken = default);
    Task UpdateAsync(string projectId, string signatureHash, StackRoute route);
    Task RemoveAsync(string projectId, string signatureHash);
    Task<bool> TryMarkRegressedAsync(StackRoute route, string eventId, CancellationToken cancellationToken = default);
}

public interface IStackRouteCache
{
    Task<IDictionary<string, CacheValue<StackRouteCacheEntry>>> GetAllAsync(IEnumerable<string> keys);
    Task<long> GetProjectGenerationAsync(string projectId);
    Task<long> AdvanceProjectGenerationAsync(string projectId);
    Task SetAsync(string key, StackRouteCacheEntry entry, TimeSpan expiresIn);
    Task SetAuthoritativeAsync(string key, StackRouteCacheEntry entry, TimeSpan expiresIn);
    Task SetAllAsync(IReadOnlyDictionary<string, StackRouteCacheEntry> entries, TimeSpan expiresIn);
    Task SetAllAuthoritativeAsync(IReadOnlyDictionary<string, StackRouteCacheEntry> entries, TimeSpan expiresIn);
    Task RemoveAsync(string key, TimeSpan expiresIn);
    Task RemoveAllAsync(IReadOnlyCollection<string> keys, TimeSpan expiresIn);
}

public sealed class StackRouteCache(ICacheClient cache, ILockProvider lockProvider, TimeProvider timeProvider) : IStackRouteCache
{
    public Task<IDictionary<string, CacheValue<StackRouteCacheEntry>>> GetAllAsync(IEnumerable<string> keys) => cache.GetAllAsync<StackRouteCacheEntry>(keys);

    public async Task<long> GetProjectGenerationAsync(string projectId)
    {
        var generation = await cache.GetAsync<long>(GetProjectGenerationKey(projectId));
        return generation.HasValue ? generation.Value : 0;
    }

    public Task<long> AdvanceProjectGenerationAsync(string projectId) =>
        cache.IncrementAsync(GetProjectGenerationKey(projectId), 1);

    public Task SetAsync(string key, StackRouteCacheEntry entry, TimeSpan expiresIn) =>
        SetAllAsync(new Dictionary<string, StackRouteCacheEntry>(1, StringComparer.Ordinal) { [key] = entry }, expiresIn);

    public Task SetAuthoritativeAsync(string key, StackRouteCacheEntry entry, TimeSpan expiresIn) =>
        SetAllAuthoritativeAsync(new Dictionary<string, StackRouteCacheEntry>(1, StringComparer.Ordinal) { [key] = entry }, expiresIn);

    public Task SetAllAsync(IReadOnlyDictionary<string, StackRouteCacheEntry> entries, TimeSpan expiresIn) =>
        UpdateGroupedAsync(entries, expiresIn, authoritative: false);

    public Task SetAllAuthoritativeAsync(IReadOnlyDictionary<string, StackRouteCacheEntry> entries, TimeSpan expiresIn) =>
        UpdateGroupedAsync(entries, expiresIn, authoritative: true);

    public Task RemoveAsync(string key, TimeSpan expiresIn) => RemoveAllAsync([key], expiresIn);

    public Task RemoveAllAsync(IReadOnlyCollection<string> keys, TimeSpan expiresIn)
    {
        if (keys.Count == 0)
            return Task.CompletedTask;

        var tombstones = keys
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                key => key,
                _ => new StackRouteCacheEntry(false, timeProvider.GetUtcNow().UtcDateTime.Ticks),
                StringComparer.Ordinal);
        return UpdateGroupedAsync(tombstones, expiresIn, authoritative: true);
    }

    private Task UpdateGroupedAsync(
        IReadOnlyDictionary<string, StackRouteCacheEntry> entries,
        TimeSpan expiresIn,
        bool authoritative)
    {
        if (entries.Count == 0)
            return Task.CompletedTask;

        return Task.WhenAll(entries
            .GroupBy(pair => GetLockKey(pair.Key), StringComparer.Ordinal)
            .Select(group => UpdateGroupAsync(group.Key, group, expiresIn, authoritative)));
    }

    private async Task UpdateGroupAsync(
        string lockKey,
        IEnumerable<KeyValuePair<string, StackRouteCacheEntry>> entries,
        TimeSpan expiresIn,
        bool authoritative)
    {
        var requested = entries.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        bool acquired = await lockProvider.TryUsingAsync(lockKey, async () =>
        {
            var current = await cache.GetAllAsync<StackRouteCacheEntry>(requested.Keys);
            var updates = new Dictionary<string, StackRouteCacheEntry>(requested.Count, StringComparer.Ordinal);
            foreach (var pair in requested)
            {
                bool hasCurrent = current.TryGetValue(pair.Key, out var cached) && cached.HasValue;
                long currentVersion = hasCurrent ? cached!.Value.Version : 0;
                if (!authoritative)
                {
                    if (!hasCurrent || pair.Value.Version > currentVersion)
                        updates[pair.Key] = pair.Value;
                    continue;
                }

                long version = Math.Max(pair.Value.Version, currentVersion + 1);
                updates[pair.Key] = pair.Value with { Version = version };
            }

            if (updates.Count == 0)
                return;

            int updated = await cache.SetAllAsync(updates, expiresIn);
            if (updated != updates.Count)
                throw new InvalidOperationException("Unable to update every stack route cache entry.");
        }, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));

        if (!acquired)
            throw new InvalidOperationException($"Unable to update stack route cache scope '{lockKey}'.");
    }

    private static string GetLockKey(string key)
    {
        int signatureSeparator = key.LastIndexOf(':');
        if (signatureSeparator <= 0)
            throw new ArgumentException("The stack route cache key is invalid.", nameof(key));

        // Route keys end in the signature hash. One generation-scoped project lock allows a cold
        // microbatch to compare and fill every route with two bulk cache calls instead of taking
        // a distributed lock and issuing a read/write command for each distinct signature.
        return String.Concat("lock:", key.AsSpan(0, signatureSeparator));
    }
    private static string GetProjectGenerationKey(string projectId) => String.Concat("stack-route:v3:generation:", projectId);
}

public sealed class StackRouteResolver(
    IStackRepository stackRepository,
    IStackRouteCache routeCache,
    ILockProvider lockProvider,
    AppOptions options) : IStackRouteResolver
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

        long projectGeneration = await routeCache.GetProjectGenerationAsync(projectId);
        var cacheKeys = distinctHashes.Select(hash => GetCacheKey(projectId, hash, projectGeneration)).ToArray();
        var cached = await routeCache.GetAllAsync(cacheKeys);
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
        {
            if (await routeCache.GetProjectGenerationAsync(projectId) != projectGeneration)
                return await ResolveAsync(projectId, distinctHashes, cancellationToken);
            return routes;
        }

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
                positiveEntries[GetCacheKey(projectId, hash, projectGeneration)] = StackRouteCacheEntry.FromRoute(route);
            }
            else
            {
                negativeEntries[GetCacheKey(projectId, hash, projectGeneration)] = new StackRouteCacheEntry(false, 0);
            }
        }

        if (positiveEntries.Count > 0)
            await routeCache.SetAllAsync(positiveEntries, options.EventIngestionV3.StackRouteCacheDuration);
        if (negativeEntries.Count > 0)
            await routeCache.SetAllAsync(negativeEntries, options.EventIngestionV3.NegativeStackRouteCacheDuration);

        // A status update may have won while the repository lookup was in flight. Re-read
        // the versioned entries so this request observes the same winner as later requests.
        var effectiveEntries = await routeCache.GetAllAsync(misses.Select(hash => GetCacheKey(projectId, hash, projectGeneration)));
        foreach (string hash in misses)
        {
            if (!effectiveEntries.TryGetValue(GetCacheKey(projectId, hash, projectGeneration), out var effective) || !effective.HasValue)
                continue;

            StackRoute? effectiveRoute = effective.Value.ToRoute();
            if (effectiveRoute is null)
                routes.Remove(hash);
            else
                routes[hash] = effectiveRoute;
        }

        // A project-wide delete can rotate the generation while the repository lookup is
        // in flight. The old fill is isolated, and this request must also re-resolve against
        // the new generation before it is allowed to route an event.
        if (await routeCache.GetProjectGenerationAsync(projectId) != projectGeneration)
            return await ResolveAsync(projectId, distinctHashes, cancellationToken);

        return routes;
    }

    public async Task UpdateAsync(string projectId, string signatureHash, StackRoute route)
    {
        long projectGeneration = await routeCache.GetProjectGenerationAsync(projectId);
        await routeCache.SetAuthoritativeAsync(GetCacheKey(projectId, signatureHash, projectGeneration), StackRouteCacheEntry.FromRoute(route), options.EventIngestionV3.StackRouteCacheDuration);
    }

    public async Task RemoveAsync(string projectId, string signatureHash)
    {
        long projectGeneration = await routeCache.GetProjectGenerationAsync(projectId);
        await routeCache.RemoveAsync(GetCacheKey(projectId, signatureHash, projectGeneration), options.EventIngestionV3.StackRouteCacheDuration);
    }

    public async Task<bool> TryMarkRegressedAsync(StackRoute route, string eventId, CancellationToken cancellationToken = default)
    {
        bool transitioned = false;
        bool acquired = await lockProvider.TryUsingAsync(String.Concat("stack-regression:", route.StackId), async () =>
        {
            var stack = await stackRepository.GetByIdAsync(route.StackId);
            if (stack is null
                || stack.Status != StackStatus.Fixed
                || stack.DateFixed != route.DateFixed
                || !String.Equals(stack.FixedInVersion, route.FixedInVersion, StringComparison.Ordinal))
                return;

            stack.Status = StackStatus.Regressed;
            stack.RegressionEventId = eventId;
            await stackRepository.SaveAsync(stack, o => o.ImmediateConsistency().Cache());
            await UpdateAsync(stack.ProjectId, stack.SignatureHash, CreateRoute(stack));
            transitioned = true;
        }, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));

        cancellationToken.ThrowIfCancellationRequested();
        if (!acquired)
            throw new InvalidOperationException($"Unable to update regression state for stack '{route.StackId}'.");

        return transitioned;
    }

    public static StackRoute CreateRoute(Stack stack) => new(
        stack.Id,
        stack.Status,
        stack.UpdatedUtc.Ticks,
        stack.FixedInVersion,
        stack.DateFixed,
        stack.OccurrencesAreCritical,
        stack.RegressionEventId,
        stack.IngestionFirstEventId);

    internal static string GetCacheKey(string projectId, string signatureHash, long projectGeneration = 0) =>
        String.Concat("stack-route:v3:v1:", projectId, ":", projectGeneration, ":", signatureHash);
}
