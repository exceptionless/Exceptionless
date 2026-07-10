using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Services;

/// <summary>
/// Cache layer over IRateNotificationRuleRepository.
/// Cache key: rate:v1:rules:project:{projectId}   TTL: 5 minutes
/// </summary>
public class RateNotificationRuleCache
{
    private readonly IRateNotificationRuleRepository _repository;
    private readonly IHybridCacheClient _cache;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public RateNotificationRuleCache(IRateNotificationRuleRepository repository, IHybridCacheClient cache)
    {
        _repository = repository;
        _cache = cache;
    }

    /// <summary>Returns all enabled, non-deleted rules for the given project (cached).</summary>
    public async Task<IReadOnlyList<RateNotificationRule>> GetEnabledRulesAsync(string projectId, CancellationToken ct = default)
    {
        string cacheKey = GetCacheKey(projectId);

        var cached = await _cache.GetAsync<List<RateNotificationRule>>(cacheKey);
        if (cached.HasValue && cached.Value is not null)
            return cached.Value;

        var results = await _repository.GetEnabledByProjectIdAsync(projectId, o => o.SearchAfterPaging().PageLimit(1000));
        var rules = new List<RateNotificationRule>();
        while (results.Documents.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            rules.AddRange(results.Documents);
            if (!await results.NextPageAsync())
                break;
        }

        await _cache.SetAsync(cacheKey, rules, CacheTtl);
        return rules;
    }

    /// <summary>Invalidates the cache for the given project.</summary>
    public Task InvalidateAsync(string projectId)
    {
        string cacheKey = GetCacheKey(projectId);
        return _cache.RemoveAsync(cacheKey);
    }

    private static string GetCacheKey(string projectId) => $"rate:v1:rules:project:{projectId}";
}
