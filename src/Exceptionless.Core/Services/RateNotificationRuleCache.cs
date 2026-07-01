using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

/// <summary>
/// Cache layer over IRateNotificationRuleRepository.
/// Cache key: rate:v1:rules:project:{projectId}   TTL: 5 minutes
/// </summary>
public class RateNotificationRuleCache
{
    private readonly IRateNotificationRuleRepository _repository;
    private readonly ICacheClient _cache;
    private readonly ILogger<RateNotificationRuleCache> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public RateNotificationRuleCache(IRateNotificationRuleRepository repository, ICacheClient cache, ILoggerFactory loggerFactory)
    {
        _repository = repository;
        _cache = cache;
        _logger = loggerFactory.CreateLogger<RateNotificationRuleCache>();
    }

    /// <summary>Returns all enabled, non-deleted rules for the given project (cached).</summary>
    public async Task<IReadOnlyList<RateNotificationRule>> GetEnabledRulesAsync(string projectId, CancellationToken ct = default)
    {
        string cacheKey = GetCacheKey(projectId);

        var cached = await _cache.GetAsync<List<RateNotificationRule>>(cacheKey);
        if (cached.HasValue && cached.Value is not null)
            return cached.Value;

        var results = await _repository.GetEnabledByProjectIdAsync(projectId, o => o.PageLimit(1000));
        var rules = results.Documents.ToList();

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
