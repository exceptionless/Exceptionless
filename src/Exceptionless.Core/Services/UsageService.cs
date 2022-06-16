using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

public class UsageService {
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICacheClient _cache;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger _logger;
    private readonly TimeSpan _bucketSize = TimeSpan.FromMinutes(5);

    public UsageService(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, ICacheClient cache, IMessagePublisher messagePublisher, ILoggerFactory loggerFactory) {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _cache = cache;
        _messagePublisher = messagePublisher;
        _logger = loggerFactory.CreateLogger<UsageService>();
    }

    public async Task SavePendingUsageAsync() {
        var utcNow = SystemClock.UtcNow;

        await SavePendingOrganizationUsageAsync(utcNow);
        await SavePendingProjectUsageAsync(utcNow);
    }

    private async Task SavePendingOrganizationUsageAsync(DateTime utcNow) {
        // default to checking the 5 previous buckets
        var lastUsageSave = utcNow.Subtract(_bucketSize * 5).Floor(_bucketSize);

        // last usage save is the last time we processed usage
        var lastUsageSaveCache = await _cache.GetAsync<DateTime>("usage:last-organization-save");
        if (lastUsageSaveCache.HasValue)
            lastUsageSave = lastUsageSaveCache.Value.Add(_bucketSize);

        var bucketUtc = lastUsageSave;
        var currentBucketUtc = utcNow.Floor(_bucketSize);

        if (bucketUtc == currentBucketUtc)
            return;

        // ideally, we would be popping a single org id off this list at a time in case something happens while we are processing these
        var organizationIdsValue = await _cache.GetListAsync<string>(GetOrganizationSetKey(bucketUtc));

        // do not process current bucket, should only be processing buckets whose window of time are complete
        while (bucketUtc < currentBucketUtc) {
            if (organizationIdsValue.HasValue) {
                // Should we wait to remove this in case there is a failure? We should just remove the organization id once processed.
                await _cache.RemoveAsync(GetOrganizationSetKey(bucketUtc));

                foreach (var organizationId in organizationIdsValue.Value) {
                    var organization = await _organizationRepository.GetByIdAsync(organizationId);
                    if (organization == null)
                        continue;

                    var bucketTotal = await _cache.GetAsync<int>(GetBucketTotalCacheKey(bucketUtc, organizationId));
                    var bucketDiscarded = await _cache.GetAsync<int>(GetBucketDiscardedCacheKey(bucketUtc, organizationId));
                    var bucketTooBig = await _cache.GetAsync<int>(GetBucketTooBigCacheKey(bucketUtc, organizationId));

                    organization.LastEventDateUtc = SystemClock.UtcNow;

                    var usage = organization.GetUsage(bucketUtc);
                    int discarded = bucketDiscarded?.Value ?? 0;
                    usage.Limit = organization.GetMaxEventsPerMonthWithBonus();
                    usage.Total += bucketTotal?.Value ?? 0;
                    usage.Blocked += discarded;
                    usage.TooBig += bucketTooBig?.Value ?? 0;

                    if (organization.HasOverage(bucketUtc)) {
                        // if we already have an overage for this time period, then increment
                        var overage = organization.GetOverage(bucketUtc);
                        overage.Total += bucketTotal?.Value ?? 0;
                        overage.Blocked += discarded;
                        overage.TooBig += bucketTooBig?.Value ?? 0;
                    } else if (discarded > 0) {
                        // start a new overage when we see discarded events and there isn't an existing overage
                        var overage = organization.GetOverage(bucketUtc);
                        overage.Total = usage.Total;
                        overage.Blocked = usage.Blocked;
                        overage.TooBig = usage.TooBig;
                    }

                    await _cache.RemoveAllAsync(new[] {
                        GetBucketTotalCacheKey(bucketUtc, organizationId),
                        GetBucketDiscardedCacheKey(bucketUtc, organizationId),
                        GetBucketTooBigCacheKey(bucketUtc, organizationId),
                        GetThrottledKey(bucketUtc, organizationId)
                    });

                    await _cache.SetAsync(GetTotalCacheKey(utcNow, organizationId), usage.Total, TimeSpan.FromHours(8));
                    await _organizationRepository.SaveAsync(organization);
                }
            }

            await _cache.SetAsync("usage:last-organization-save", bucketUtc, TimeSpan.FromHours(8));

            bucketUtc = bucketUtc.Add(_bucketSize);
            organizationIdsValue = await _cache.GetListAsync<string>(GetOrganizationSetKey(bucketUtc));
        }
    }

    public async Task HandleOrganizationChange(Organization modified, Organization original) {
        var utcNow = SystemClock.UtcNow;

        await _cache.RemoveAsync($"usage:limits:{modified.Id}");

        // remove is throttled flag
        if (modified.GetMaxEventsPerMonthWithBonus() > original.GetMaxEventsPerMonthWithBonus())
            await _cache.RemoveAsync(GetThrottledKey(utcNow, modified.Id));
    }

    private async Task SavePendingProjectUsageAsync(DateTime utcNow) {
        // default to checking the 5 previous buckets
        var lastUsageSave = utcNow.Subtract(_bucketSize * 5).Floor(_bucketSize);

        // last usage save is the last time we processed usage
        var lastUsageSaveCache = await _cache.GetAsync<DateTime>("usage:last-project-save");
        if (lastUsageSaveCache.HasValue)
            lastUsageSave = lastUsageSaveCache.Value.Add(_bucketSize);

        var bucketUtc = lastUsageSave;
        var currentBucketUtc = utcNow.Floor(_bucketSize);

        if (bucketUtc == currentBucketUtc)
            return;

        // ideally, we would be popping a single org id off this list at a time in case something happens while we are processing these
        var projectIdsValue = await _cache.GetListAsync<string>(GetProjectSetKey(bucketUtc));

        // do not process current bucket, should only be processing buckets whose window of time are complete
        while (bucketUtc < currentBucketUtc) {
            if (projectIdsValue.HasValue) {
                await _cache.RemoveAsync(GetProjectSetKey(bucketUtc));

                foreach (var projectId in projectIdsValue.Value) {
                    var project = await _projectRepository.GetByIdAsync(projectId);
                    if (project == null)
                        continue;

                    var bucketTotal = await _cache.GetAsync<int>(GetBucketTotalCacheKey(bucketUtc, project.OrganizationId, projectId));
                    var bucketDiscarded = await _cache.GetAsync<int>(GetBucketDiscardedCacheKey(bucketUtc, project.OrganizationId, projectId));
                    var bucketTooBig = await _cache.GetAsync<int>(GetBucketTooBigCacheKey(bucketUtc, project.OrganizationId, projectId));

                    project.LastEventDateUtc = SystemClock.UtcNow;

                    var context = (OrganizationId: project.OrganizationId, Organization: (Organization)null);
                    int maxEventsPerMonth = await GetMaxEventsPerMonthAsync(context);

                    var usage = project.GetUsage(bucketUtc);
                    int discarded = bucketDiscarded?.Value ?? 0;
                    usage.Limit = maxEventsPerMonth;
                    usage.Total += bucketTotal?.Value ?? 0;
                    usage.Blocked += discarded;
                    usage.TooBig += bucketTooBig?.Value ?? 0;

                    if (project.HasOverage(bucketUtc)) {
                        // if we already have an overage for this time period, then increment
                        var overage = project.GetOverage(bucketUtc);
                        overage.Total += bucketTotal?.Value ?? 0;
                        overage.Blocked += discarded;
                        overage.TooBig += bucketTooBig?.Value ?? 0;
                    } else if (discarded > 0) {
                        // start a new overage when we see discarded events and there isn't an existing overage
                        var overage = project.GetOverage(bucketUtc);
                        overage.Total = usage.Total;
                        overage.Blocked = usage.Blocked;
                        overage.TooBig = usage.TooBig;
                    }

                    await _cache.RemoveAllAsync(new[] {
                        GetBucketTotalCacheKey(bucketUtc, project.OrganizationId, projectId),
                        GetBucketDiscardedCacheKey(bucketUtc, project.OrganizationId, projectId),
                        GetBucketTooBigCacheKey(bucketUtc, project.OrganizationId, projectId)
                    });

                    await _cache.SetAsync(GetTotalCacheKey(utcNow, project.OrganizationId, projectId), usage.Total, TimeSpan.FromHours(8));

                    await _projectRepository.SaveAsync(project);
                }
            }

            await _cache.SetAsync("usage:last-project-save", bucketUtc, TimeSpan.FromHours(8));

            bucketUtc = bucketUtc.Add(_bucketSize);
            projectIdsValue = await _cache.GetListAsync<string>(GetProjectSetKey(bucketUtc));
        }
    }

    public ValueTask<int> GetMaxEventsPerMonthAsync(string organizationId) {
        return GetMaxEventsPerMonthAsync((organizationId, null));
    }

    private async ValueTask<int> GetMaxEventsPerMonthAsync((string OrganizationId, Organization Organization) context) {
        // maybe use an in memory cache for this
        int maxEventsPerMonth = 0;
        var maxEventsPerMonthCache = await _cache.GetAsync<int>($"usage:limits:{context.OrganizationId}");
        if (maxEventsPerMonthCache.HasValue) {
            maxEventsPerMonth = maxEventsPerMonthCache.Value;
        } else {
            if (context.Organization == null)
                context.Organization = await _organizationRepository.GetByIdAsync(context.OrganizationId, o => o.Cache());

            if (context.Organization != null) {
                maxEventsPerMonth = context.Organization.GetMaxEventsPerMonthWithBonus();
                await _cache.SetAsync($"usage:limits:{context.OrganizationId}", maxEventsPerMonth, TimeSpan.FromDays(1));
            }
        }

        return maxEventsPerMonth;
    }

    public async Task<UsageInfoResponse> GetUsageAsync(string organizationId) {
        var utcNow = SystemClock.UtcNow;

        // default to checking just the previous bucket
        var lastUsageSave = utcNow.Subtract(_bucketSize).Floor(_bucketSize);

        // last usage save is the last time we processed usage
        var lastUsageSaveCache = await _cache.GetAsync<DateTime>("usage:last-organization-save");
        if (lastUsageSaveCache.HasValue)
            lastUsageSave = lastUsageSaveCache.Value.Add(_bucketSize);

        var bucketUtc = lastUsageSave;
        var currentBucketUtc = utcNow.Floor(_bucketSize);

        var organization = await _organizationRepository.GetByIdAsync(organizationId, o => o.Cache());

        var isThrottled = await _cache.GetAsync<bool>(GetThrottledKey(currentBucketUtc, organizationId));
        var currentUsage = organization.GetCurrentUsage();
        var usage = new UsageInfoResponse {
            Date = currentUsage.Date,
            Limit = currentUsage.Limit,
            IsThrottled = isThrottled?.Value ?? false,
            Total = currentUsage.Total,
            Blocked = currentUsage.Blocked,
            TooBig = currentUsage.TooBig
        };

        while (bucketUtc <= currentBucketUtc) {
            // get current bucket counters
            var bucketTotal = await _cache.GetAsync<int>(GetBucketTotalCacheKey(bucketUtc, organizationId));
            usage.Total += bucketTotal?.Value ?? 0;

            var bucketDiscarded = await _cache.GetAsync<int>(GetBucketDiscardedCacheKey(bucketUtc, organizationId));
            usage.Blocked += bucketDiscarded?.Value ?? 0;

            var bucketTooBig = await _cache.GetAsync<int>(GetBucketTooBigCacheKey(bucketUtc, organizationId));
            usage.TooBig += bucketTooBig?.Value ?? 0;

            if (bucketUtc == currentBucketUtc && (bucketDiscarded?.Value ?? 0) > 0) {
                usage.Overage = new OverageInfo {
                    Date = utcNow.Floor(_bucketSize),
                    Total = bucketTotal?.Value ?? 0,
                    Blocked = bucketDiscarded?.Value ?? 0,
                    TooBig = bucketTooBig?.Value ?? 0
                };
            }

            bucketUtc = bucketUtc.Add(_bucketSize);
        }

        return usage;
    }

    public async Task<int> GetEventsLeftAsync(string organizationId) {
        var utcNow = SystemClock.UtcNow;

        var context = (OrganizationId: organizationId, Organization: (Organization)null);
        int maxEventsPerMonth = await GetMaxEventsPerMonthAsync(context);

        // check for unlimited (-1) events
        if (maxEventsPerMonth < 0)
            return Int32.MaxValue;

        int currentTotal;
        var currentTotalCache = await _cache.GetAsync<int>(GetTotalCacheKey(utcNow, organizationId));
        if (currentTotalCache.HasValue) {
            currentTotal = currentTotalCache.Value;
        } else {
            if (context.Organization is null)
                context.Organization = await _organizationRepository.GetByIdAsync(organizationId, o => o.Cache());

            currentTotal = context.Organization.GetCurrentUsage().Total;
            await _cache.SetAsync(GetTotalCacheKey(utcNow, organizationId), currentTotal, TimeSpan.FromHours(8));
        }

        // if already over limit, return
        if (currentTotal >= maxEventsPerMonth)
            return 0;

        // get current bucket counter and add it to total
        var bucketTotal = await _cache.GetAsync<int>(GetBucketTotalCacheKey(utcNow, organizationId));
        if (bucketTotal.HasValue)
            currentTotal += bucketTotal.Value;

        // get previous bucket counter and add it to total since it might not be saved yet
        var previousBucketTotal = await _cache.GetAsync<int>(GetBucketTotalCacheKey(utcNow.Subtract(_bucketSize), organizationId));
        if (previousBucketTotal.HasValue)
            currentTotal += previousBucketTotal.Value;

        // check to see if adding this bucket puts the org over the limit
        if (currentTotal >= maxEventsPerMonth)
            return 0;

        // get a bucket level limit to help spread the events out more evenly (allows bursting)
        int bucketLimit = GetBucketEventLimit(maxEventsPerMonth);
        int eventsLeftInBucket = bucketLimit - (bucketTotal?.Value ?? 0);

        return eventsLeftInBucket;
    }

    public async Task IncrementTotalAsync(string organizationId, string projectId, int eventCount = 1) {
        if (eventCount <= 0)
            return;

        var utcNow = SystemClock.UtcNow;

        var bucketTotal = await _cache.IncrementAsync(GetBucketTotalCacheKey(utcNow, organizationId), eventCount, TimeSpan.FromHours(8));
        await _cache.IncrementAsync(GetBucketTotalCacheKey(utcNow, organizationId, projectId), eventCount, TimeSpan.FromHours(8));

        await _cache.ListAddAsync(GetOrganizationSetKey(utcNow), organizationId, TimeSpan.FromHours(8));
        await _cache.ListAddAsync(GetProjectSetKey(utcNow), projectId, TimeSpan.FromHours(8));

        var maxEventsPerMonth = await GetMaxEventsPerMonthAsync(organizationId);
        int bucketLimit = GetBucketEventLimit(maxEventsPerMonth);

        var currentTotalCache = await _cache.GetAsync<int>(GetTotalCacheKey(utcNow, organizationId));
        if (currentTotalCache.HasValue) {
            long monthTotal = currentTotalCache.Value + bucketTotal;
            if (monthTotal >= maxEventsPerMonth && monthTotal - maxEventsPerMonth < eventCount)
                await _messagePublisher.PublishAsync(new PlanOverage { OrganizationId = organizationId });
        }
        
        if (bucketTotal >= bucketLimit && bucketTotal - bucketLimit < eventCount) {
            // org will be throttled during the current bucket of time
            await _messagePublisher.PublishAsync(new PlanOverage { OrganizationId = organizationId, IsHourly = true });
            await _cache.SetAsync(GetThrottledKey(utcNow, organizationId), true, TimeSpan.FromMinutes(5));
        }
    }

    public async Task IncrementDiscardedAsync(string organizationId, string projectId, int eventCount = 1) {
        if (eventCount <= 0)
            return;

        var utcNow = SystemClock.UtcNow;

        await _cache.IncrementAsync(GetBucketDiscardedCacheKey(utcNow, organizationId), eventCount, TimeSpan.FromHours(8));
        await _cache.IncrementAsync(GetBucketDiscardedCacheKey(utcNow, organizationId, projectId), eventCount, TimeSpan.FromHours(8));

        await _cache.ListAddAsync(GetOrganizationSetKey(utcNow), organizationId, TimeSpan.FromHours(8));
        await _cache.ListAddAsync(GetProjectSetKey(utcNow), projectId, TimeSpan.FromHours(8));

        AppDiagnostics.EventsDiscarded.Add(eventCount);
    }

    public async Task IncrementTooBigAsync(string organizationId, string projectId) {
        var utcNow = SystemClock.UtcNow;

        await _cache.IncrementAsync(GetBucketTooBigCacheKey(utcNow, organizationId), 1, TimeSpan.FromHours(8));
        await _cache.IncrementAsync(GetBucketTooBigCacheKey(utcNow, organizationId, projectId), 1, TimeSpan.FromHours(8));

        await _cache.ListAddAsync(GetOrganizationSetKey(utcNow), organizationId, TimeSpan.FromHours(8));
        await _cache.ListAddAsync(GetProjectSetKey(utcNow), projectId, TimeSpan.FromHours(8));
        
        AppDiagnostics.PostTooBig.Add(1);
    }

    private int GetBucketEventLimit(int maxEventsPerMonth) {
        if (maxEventsPerMonth < 5000)
            return maxEventsPerMonth;

        var utcNow = SystemClock.UtcNow;
        var timeLeftInMonth = utcNow.EndOfMonth() - utcNow;
        if (timeLeftInMonth < TimeSpan.FromDays(1))
            return maxEventsPerMonth;

        var bucketsLeftInMonth = timeLeftInMonth / _bucketSize;

        // allow boosting to 10x the max / bucket if the events were divided evenly
        return (int)Math.Ceiling((maxEventsPerMonth / bucketsLeftInMonth) * 10);
    }

    private string GetTotalCacheKey(DateTime utcTime, string organizationId, string projectId = null) {
        int bucket = GetTotalBucket(utcTime);

        if (String.IsNullOrEmpty(projectId))
            return $"usage:total:{bucket}:{organizationId}:total";

        return $"usage:total:{bucket}:{organizationId}:{projectId}:total";
    }

    private string GetBucketTotalCacheKey(DateTime utcTime, string organizationId, string projectId = null) {
        int bucket = GetCurrentBucket(utcTime);

        if (String.IsNullOrEmpty(projectId))
            return $"usage:{bucket}:{organizationId}:total";

        return $"usage:{bucket}:{organizationId}:{projectId}:total";
    }

    private string GetBucketDiscardedCacheKey(DateTime utcTime, string organizationId, string projectId = null) {
        int bucket = GetCurrentBucket(utcTime);

        if (String.IsNullOrEmpty(projectId))
            return $"usage:{bucket}:{organizationId}:discarded";

        return $"usage:{bucket}:{organizationId}:{projectId}:discarded";
    }

    private string GetBucketTooBigCacheKey(DateTime utcTime, string organizationId, string projectId = null) {
        int bucket = GetCurrentBucket(utcTime);

        if (String.IsNullOrEmpty(projectId))
            return $"usage:{bucket}:{organizationId}:toobig";

        return $"usage:{bucket}:{organizationId}:{projectId}:toobig";
    }

    private string GetOrganizationSetKey(DateTime utcTime) {
        int bucket = GetCurrentBucket(utcTime);
        return $"usage:{bucket}:organizations";
    }

    private string GetThrottledKey(DateTime utcTime, string organizationId) {
        int bucket = GetCurrentBucket(utcTime);
        return $"usage:{bucket}:{organizationId}:throttled";
    }

    private string GetProjectSetKey(DateTime utcTime) {
        int bucket = GetCurrentBucket(utcTime);
        return $"usage:{bucket}:projects";
    }

    private int GetCurrentBucket(DateTime utcTime) => utcTime.Floor(_bucketSize).ToEpoch();
    private int GetTotalBucket(DateTime utcTime) => utcTime.StartOfMonth().ToEpoch();
}
