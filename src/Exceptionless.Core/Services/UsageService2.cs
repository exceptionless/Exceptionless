using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

public class UsageService2 {
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICacheClient _cache;
    private readonly IMessagePublisher _messagePublisher;
    private readonly BillingPlans _plans;
    private readonly ILogger<UsageService> _logger;

    public UsageService2(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, ICacheClient cache, IMessagePublisher messagePublisher, BillingPlans plans, ILoggerFactory loggerFactory = null) {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _cache = cache;
        _messagePublisher = messagePublisher;
        _plans = plans;
        _logger = loggerFactory.CreateLogger<UsageService>();
    }

    public async Task SaveUsageInfo() {
        // start at most recent bucket that is before the current bucket
        // keep going back to previous buckets until we don't find a list

        // store last time save was called so we know where to start looking for buckets
        // especially in cases where the entire service is down for some period of time

        var bucketUtc = SystemClock.UtcNow.AddMinutes(-5);

        // ideally, we would be popping a single org id off this list at a time in case something happens while we are processing these
        var organizationIdsValue = await _cache.GetListAsync<string>(GetOrganizationSetKey(bucketUtc));

        while (organizationIdsValue.HasValue) {
            await _cache.RemoveAsync(GetOrganizationSetKey(bucketUtc));

            foreach (var organizationId in organizationIdsValue.Value) {
                var organization = await _organizationRepository.GetByIdAsync(organizationId);

                var bucketTotal = await _cache.GetAsync<int>(GetBucketTotalCacheKey(bucketUtc, organizationId));
                var bucketDiscarded = await _cache.GetAsync<int>(GetBucketDiscardedCacheKey(bucketUtc, organizationId));
                var bucketTooBig = await _cache.GetAsync<int>(GetBucketTooBigCacheKey(bucketUtc, organizationId));

                organization.Usage.IncrementUsage(bucketUtc, bucketTotal?.Value ?? 0, bucketDiscarded?.Value ?? 0, bucketTooBig?.Value ?? 0, organization.GetMaxEventsPerMonthWithBonus(), TimeSpan.FromDays(366));
                organization.LastEventDateUtc = SystemClock.UtcNow;
                // add these to the org usage
                // update current total
            }

            bucketUtc = bucketUtc.SubtractMinutes(-5);
            organizationIdsValue = await _cache.GetListAsync<string>(GetOrganizationSetKey(bucketUtc));
        }
    }

    public async Task<int> GetEventsLeftAsync(string organizationId, string projectId) {
        var utcNow = SystemClock.UtcNow;

        var currentTotal = (await _cache.GetAsync<int>(GetTotalCacheKey(utcNow, organizationId)))?.Value ?? 0;
        // if no current total, then we need to get it and populate it

        var bucketTotal = await _cache.GetAsync<int>(GetBucketTotalCacheKey(utcNow, organizationId));
        if (bucketTotal.HasValue)
            currentTotal += bucketTotal.Value;

        // get current period limit
        // ideally, I don't want to have to load the entire org document to get this info as we want to reject events as cheaply as possible
        int periodLimit = 10000;

        return periodLimit - currentTotal;
    }

    public async Task IncrementTotalAsync(string organizationId, string projectId, int eventCount = 1) {
        var utcNow = SystemClock.UtcNow;

        await _cache.IncrementAsync(GetBucketTotalCacheKey(utcNow, organizationId), eventCount, TimeSpan.FromDays(1));
        await _cache.IncrementAsync(GetBucketTotalCacheKey(utcNow, organizationId, projectId), eventCount, TimeSpan.FromDays(1));

        await _cache.ListAddAsync(GetOrganizationSetKey(utcNow), organizationId);
        await _cache.ListAddAsync(GetProjectSetKey(utcNow), projectId);
    }

    public async Task IncrementDiscardedAsync(string organizationId, string projectId, int eventCount = 1) {
        var utcNow = SystemClock.UtcNow;

        await _cache.IncrementAsync(GetBucketDiscardedCacheKey(utcNow, organizationId), eventCount, TimeSpan.FromDays(1));
        await _cache.IncrementAsync(GetBucketDiscardedCacheKey(utcNow, organizationId, projectId), eventCount, TimeSpan.FromDays(1));

        await _cache.ListAddAsync(GetOrganizationSetKey(utcNow), organizationId);
        await _cache.ListAddAsync(GetProjectSetKey(utcNow), projectId);
    }

    public async Task IncrementTooBigAsync(string organizationId, string projectId) {
        var utcNow = SystemClock.UtcNow;

        await _cache.IncrementAsync(GetBucketTooBigCacheKey(utcNow, organizationId), 1, TimeSpan.FromDays(1));
        await _cache.IncrementAsync(GetBucketTooBigCacheKey(utcNow, organizationId, projectId), 1, TimeSpan.FromDays(1));

        await _cache.ListAddAsync(GetOrganizationSetKey(utcNow), organizationId);
        await _cache.ListAddAsync(GetProjectSetKey(utcNow), projectId);
    }

    private string GetTotalCacheKey(DateTime utcTime, string organizationId, string projectId = null) {
        int bucket = GetTotalBucket(utcTime);

        if (String.IsNullOrEmpty(projectId))
            return String.Concat($"usage:total:{bucket}:{organizationId}:total");

        return String.Concat($"usage:total:{bucket}:{organizationId}:{projectId}:total");
    }

    private string GetBucketTotalCacheKey(DateTime utcTime, string organizationId, string projectId = null) {
        int bucket = GetCurrentBucket(utcTime);

        if (String.IsNullOrEmpty(projectId))
            return String.Concat($"usage:{bucket}:{organizationId}:total");

        return String.Concat($"usage:{bucket}:{organizationId}:{projectId}:total");
    }

    private string GetBucketDiscardedCacheKey(DateTime utcTime, string organizationId, string projectId = null) {
        int bucket = GetCurrentBucket(utcTime);

        if (String.IsNullOrEmpty(projectId))
            return String.Concat($"usage:{bucket}:{organizationId}:discarded");

        return String.Concat($"usage:{bucket}:{organizationId}:{projectId}:discarded");
    }

    private string GetBucketTooBigCacheKey(DateTime utcTime, string organizationId, string projectId = null) {
        int bucket = GetCurrentBucket(utcTime);

        if (String.IsNullOrEmpty(projectId))
            return String.Concat($"usage:{bucket}:{organizationId}:toobig");

        return String.Concat($"usage:{bucket}:{organizationId}:{projectId}:toobig");
    }

    private string GetOrganizationSetKey(DateTime utcTime) {
        int bucket = GetCurrentBucket(utcTime);
        return String.Concat($"usage:{bucket}:organizations");
    }

    private string GetProjectSetKey(DateTime utcTime) {
        int bucket = GetCurrentBucket(utcTime);
        return String.Concat($"usage:{bucket}:projects");
    }

    private int GetCurrentBucket(DateTime utcTime) => utcTime.Floor(TimeSpan.FromMinutes(5)).ToEpoch();
    private int GetTotalBucket(DateTime utcTime) => utcTime.StartOfMonth().ToEpoch();
}
