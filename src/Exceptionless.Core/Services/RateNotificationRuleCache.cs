using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Services;

/// <summary>
/// Compiles and caches the project counter plan used by event ingestion.
/// Cache key: rate:v2:counter-plan:project:{projectId}   TTL: 5 minutes
/// </summary>
public class RateNotificationRuleCache : IStartupAction
{
    private readonly IRateNotificationRuleRepository _repository;
    private readonly IHybridCacheClient _cache;
    private readonly SemaphoreSlim[] _cacheGates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public RateNotificationRuleCache(IRateNotificationRuleRepository repository, IHybridCacheClient cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public Task RunAsync(CancellationToken shutdownToken = default)
    {
        _repository.DocumentsChanged.AddHandler(OnRulesChangedAsync);
        return Task.CompletedTask;
    }

    /// <summary>Returns the compiled counter plan for all enabled rules in the project.</summary>
    public async Task<RateNotificationCounterPlan> GetCounterPlanAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        string cacheKey = GetCacheKey(projectId);

        var cached = await _cache.GetAsync<RateNotificationCounterPlan>(cacheKey);
        if (cached.HasValue && cached.Value is not null)
            return cached.Value;

        var cacheGate = GetCacheGate(projectId);
        await cacheGate.WaitAsync(ct);
        try
        {
            cached = await _cache.GetAsync<RateNotificationCounterPlan>(cacheKey);
            if (cached.HasValue && cached.Value is not null)
                return cached.Value;

            var rules = new List<RateNotificationRule>();
            var results = await _repository.GetEnabledByProjectIdAsync(projectId, o => o.SearchAfterPaging().PageLimit(500));
            do
            {
                ct.ThrowIfCancellationRequested();
                rules.AddRange(results.Documents);
            } while (await results.NextPageAsync());

            var plan = RateNotificationCounterPlan.Compile(projectId, rules);
            await _cache.SetAsync(cacheKey, plan, CacheTtl);
            return plan;
        }
        finally
        {
            cacheGate.Release();
        }
    }

    /// <summary>Invalidates the cache for the given project.</summary>
    public async Task InvalidateAsync(string projectId)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        var cacheGate = GetCacheGate(projectId);
        await cacheGate.WaitAsync();
        try
        {
            await _cache.RemoveAsync(GetCacheKey(projectId));
        }
        finally
        {
            cacheGate.Release();
        }
    }

    private Task OnRulesChangedAsync(object sender, DocumentsChangeEventArgs<RateNotificationRule> args)
    {
        var projectIds = args.Documents
            .SelectMany(document => new[] { document.Original?.ProjectId, document.Value?.ProjectId })
            .Where(projectId => !String.IsNullOrEmpty(projectId))
            .Distinct(StringComparer.Ordinal)
            .Select(projectId => InvalidateAsync(projectId!));

        return Task.WhenAll(projectIds);
    }

    private SemaphoreSlim GetCacheGate(string projectId)
    {
        uint hash = unchecked((uint)StringComparer.Ordinal.GetHashCode(projectId));
        return _cacheGates[hash % _cacheGates.Length];
    }

    private static string GetCacheKey(string projectId) => $"rate:v2:counter-plan:project:{projectId}";
}
