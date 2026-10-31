using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Lock;
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
    private readonly ICacheClient _cache;
    private readonly ILockProvider _lockProvider;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(15);

    public RateNotificationRuleCache(IRateNotificationRuleRepository repository, ICacheClient cache, ILockProvider lockProvider)
    {
        _repository = repository;
        _cache = cache;
        _lockProvider = lockProvider;
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

        using var lockTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lockTimeout.CancelAfter(LockWait);
        await using var cacheLock = await _lockProvider.AcquireAsync(GetLockKey(projectId), LockTtl, lockTimeout.Token);
        cached = await _cache.GetAsync<RateNotificationCounterPlan>(cacheKey);
        if (cached.HasValue && cached.Value is not null)
            return cached.Value;

        var rules = new List<RateNotificationRule>();
        var results = await _repository.GetEnabledByProjectIdAsync(projectId, o => o.SearchAfterPaging().PageLimit(500));
        do
        {
            ct.ThrowIfCancellationRequested();
            await cacheLock.RenewAsync(LockTtl);
            rules.AddRange(results.Documents);
        } while (await results.NextPageAsync());

        var plan = RateNotificationCounterPlan.Compile(projectId, rules);
        await _cache.SetAsync(cacheKey, plan, CacheTtl);
        return plan;
    }

    /// <summary>Invalidates the cache for the given project.</summary>
    public async Task InvalidateAsync(string projectId)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        // Share the fill lock across replicas so an older query cannot repopulate an invalidated plan.
        await using var cacheLock = await _lockProvider.AcquireAsync(GetLockKey(projectId), LockTtl, LockWait);
        await _cache.RemoveAsync(GetCacheKey(projectId));
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

    private static string GetLockKey(string projectId) => $"rate:counter-plan:project:{projectId}";
    private static string GetCacheKey(string projectId) => $"rate:v2:counter-plan:project:{projectId}";
}
