using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

public class UsageService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICacheClient _cache;
    private readonly IMessagePublisher _messagePublisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly TimeSpan _bucketSize = TimeSpan.FromMinutes(5);

    public UsageService(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, ICacheClient cache, IMessagePublisher messagePublisher,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _cache = cache;
        _messagePublisher = messagePublisher;
        _timeProvider = timeProvider;
        _logger = loggerFactory.CreateLogger<UsageService>();
    }

    public async Task SavePendingUsageAsync()
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        await SavePendingOrganizationUsageAsync(utcNow);
        await SavePendingProjectUsageAsync(utcNow);
    }

    private async Task SavePendingOrganizationUsageAsync(DateTime utcNow)
    {
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
        while (bucketUtc < currentBucketUtc)
        {
            if (organizationIdsValue.HasValue)
            {
                // Should we wait to remove this in case there is a failure? We should just remove the organization id once processed.
                await _cache.RemoveAsync(GetOrganizationSetKey(bucketUtc));

                foreach (string? organizationId in organizationIdsValue.Value)
                {
                    var organization = await _organizationRepository.GetByIdAsync(organizationId);
                    if (organization is null)
                        continue;

                    _logger.LogInformation("Saving org ({OrganizationId}-{OrganizationName}) event usage for time bucket: {BucketUtc}...", organizationId, organization.Name, bucketUtc);

                    var bucketTotal = await _cache.GetAsync<int>(GetBucketTotalCacheKey(bucketUtc, organizationId));
                    var bucketBlocked = await _cache.GetAsync<int>(GetBucketBlockedCacheKey(bucketUtc, organizationId));
                    var bucketDiscarded = await _cache.GetAsync<int>(GetBucketDiscardedCacheKey(bucketUtc, organizationId));
                    var bucketTooBig = await _cache.GetAsync<int>(GetBucketTooBigCacheKey(bucketUtc, organizationId));

                    organization.LastEventDateUtc = _timeProvider.GetUtcNow().UtcDateTime;

                    var usage = organization.GetUsage(bucketUtc, _timeProvider);
                    usage.Limit = organization.GetMaxEventsPerMonthWithBonus(_timeProvider);
                    usage.Total += bucketTotal?.Value ?? 0;
                    usage.Blocked += bucketBlocked?.Value ?? 0;
                    usage.Discarded += bucketDiscarded?.Value ?? 0;
                    usage.TooBig += bucketTooBig?.Value ?? 0;

                    var hourlyUsage = organization.GetHourlyUsage(bucketUtc);
                    hourlyUsage.Total += bucketTotal?.Value ?? 0;
                    hourlyUsage.Blocked += bucketBlocked?.Value ?? 0;
                    hourlyUsage.Discarded += bucketDiscarded?.Value ?? 0;
                    hourlyUsage.TooBig += bucketTooBig?.Value ?? 0;

                    organization.TrimUsage(_timeProvider);

                    await _cache.RemoveAllAsync(new[] {
                        GetBucketTotalCacheKey(bucketUtc, organizationId),
                        GetBucketBlockedCacheKey(bucketUtc, organizationId),
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

    private async Task SavePendingProjectUsageAsync(DateTime utcNow)
    {
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
        while (bucketUtc < currentBucketUtc)
        {
            if (projectIdsValue.HasValue)
            {
                await _cache.RemoveAsync(GetProjectSetKey(bucketUtc));

                foreach (string? projectId in projectIdsValue.Value)
                {
                    var project = await _projectRepository.GetByIdAsync(projectId);
                    if (project is null)
                        continue;

                    _logger.LogInformation("Saving project ({ProjectId}-{ProjectName}) event usage for time bucket: {BucketUtc}...", projectId, project.Name, bucketUtc);

                    var bucketTotal = await _cache.GetAsync<int>(GetBucketTotalCacheKey(bucketUtc, project.OrganizationId, projectId));
                    var bucketBlocked = await _cache.GetAsync<int>(GetBucketBlockedCacheKey(bucketUtc, project.OrganizationId, projectId));
                    var bucketDiscarded = await _cache.GetAsync<int>(GetBucketDiscardedCacheKey(bucketUtc, project.OrganizationId, projectId));
                    var bucketTooBig = await _cache.GetAsync<int>(GetBucketTooBigCacheKey(bucketUtc, project.OrganizationId, projectId));

                    project.LastEventDateUtc = _timeProvider.GetUtcNow().UtcDateTime;

                    (string OrganizationId, Organization? Organization) context = (OrganizationId: project.OrganizationId, Organization: null);
                    int maxEventsPerMonth = await GetMaxEventsPerMonthAsync(context);

                    var usage = project.GetUsage(bucketUtc);
                    usage.Limit = maxEventsPerMonth;
                    usage.Total += bucketTotal?.Value ?? 0;
                    usage.Blocked += bucketBlocked?.Value ?? 0;
                    usage.Discarded += bucketDiscarded?.Value ?? 0;
                    usage.TooBig += bucketTooBig?.Value ?? 0;

                    var hourlyUsage = project.GetHourlyUsage(bucketUtc);
                    hourlyUsage.Total += bucketTotal?.Value ?? 0;
                    hourlyUsage.Blocked += bucketBlocked?.Value ?? 0;
                    hourlyUsage.Discarded += bucketDiscarded?.Value ?? 0;
                    hourlyUsage.TooBig += bucketTooBig?.Value ?? 0;

                    project.TrimUsage(_timeProvider);

                    await _cache.RemoveAllAsync(new[] {
                        GetBucketTotalCacheKey(bucketUtc, project.OrganizationId, projectId),
                        GetBucketDiscardedCacheKey(bucketUtc, project.OrganizationId, projectId),
                        GetBucketBlockedCacheKey(bucketUtc, project.OrganizationId, projectId),
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

    public async Task HandleOrganizationChange(Organization modified, Organization original)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        await _cache.RemoveAsync($"usage:limits:{modified.Id}");

        int modifiedMaxEvents = modified.GetMaxEventsPerMonthWithBonus(_timeProvider);
        int originalMaxEvents = original.GetMaxEventsPerMonthWithBonus(_timeProvider);

        if (modifiedMaxEvents == originalMaxEvents)
            return;

        if (modifiedMaxEvents > originalMaxEvents)
        {
            // remove is throttled flag
            await _cache.RemoveAsync(GetThrottledKey(utcNow, modified.Id));
        }
        else
        {
            var bucketTotal = await _cache.GetAsync<int>(GetBucketTotalCacheKey(utcNow, modified.Id));
            if (!bucketTotal.HasValue)
                return;

            int bucketLimit = GetBucketEventLimit(modifiedMaxEvents);

            // unlimited
            if (bucketLimit < 0)
                return;

            if (bucketTotal.Value >= bucketLimit)
            {
                await _messagePublisher.PublishAsync(new PlanOverage { OrganizationId = modified.Id, IsHourly = true });
                await _cache.SetAsync(GetThrottledKey(utcNow, modified.Id), true, TimeSpan.FromMinutes(5));
            }
        }
    }

    public ValueTask<int> GetMaxEventsPerMonthAsync(string organizationId)
    {
        return GetMaxEventsPerMonthAsync((organizationId, null));
    }

    private async ValueTask<int> GetMaxEventsPerMonthAsync((string OrganizationId, Organization? Organization) context)
    {
        // maybe use an in memory cache for this
        int maxEventsPerMonth = 0;
        var maxEventsPerMonthCache = await _cache.GetAsync<int>($"usage:limits:{context.OrganizationId}");
        if (maxEventsPerMonthCache.HasValue)
        {
            maxEventsPerMonth = maxEventsPerMonthCache.Value;
        }
        else
        {
            if (context.Organization is null)
                context.Organization = await _organizationRepository.GetByIdAsync(context.OrganizationId, o => o.Cache());

            if (context.Organization is not null)
            {
                maxEventsPerMonth = context.Organization.GetMaxEventsPerMonthWithBonus(_timeProvider);
                await _cache.SetAsync($"usage:limits:{context.OrganizationId}", maxEventsPerMonth, TimeSpan.FromDays(1));
            }
        }

        return maxEventsPerMonth;
    }

    public async Task<UsageInfoResponse> GetUsageAsync(string organizationId, string? projectId = null)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        // default to checking just the previous bucket
        var lastUsageSave = utcNow.Subtract(_bucketSize).Floor(_bucketSize);

        // last usage save is the last time we processed usage
        var lastUsageSaveCache = await _cache.GetAsync<DateTime>(projectId is null ? "usage:last-organization-save" : "usage:last-project-save");
        if (lastUsageSaveCache.HasValue)
            lastUsageSave = lastUsageSaveCache.Value.Add(_bucketSize);

        var bucketUtc = lastUsageSave;
        var currentBucketUtc = utcNow.Floor(_bucketSize);
        var isThrottled = await _cache.GetAsync<bool>(GetThrottledKey(currentBucketUtc, organizationId));

        UsageInfoResponse usage;
        if (projectId is null)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId, o => o.Cache());
            organization.TrimUsage(_timeProvider);

            usage = new UsageInfoResponse
            {
                IsThrottled = isThrottled?.Value ?? false,
                CurrentUsage = organization.GetCurrentUsage(_timeProvider),
                CurrentHourUsage = organization.GetCurrentHourlyUsage(_timeProvider)
            };
        }
        else
        {
            var project = await _projectRepository.GetByIdAsync(projectId, o => o.Cache());
            project.TrimUsage(_timeProvider);

            usage = new UsageInfoResponse
            {
                IsThrottled = isThrottled?.Value ?? false,
                CurrentUsage = project.GetCurrentUsage(_timeProvider),
                CurrentHourUsage = project.GetCurrentHourlyUsage(_timeProvider)
            };
        }

        while (bucketUtc <= currentBucketUtc)
        {
            // get current bucket counters
            var bucketTotal = await _cache.GetAsync<int>(GetBucketTotalCacheKey(bucketUtc, organizationId, projectId));
            usage.CurrentUsage.Total += bucketTotal?.Value ?? 0;
            usage.CurrentHourUsage.Total += bucketTotal?.Value ?? 0;

            var bucketBlocked = await _cache.GetAsync<int>(GetBucketBlockedCacheKey(bucketUtc, organizationId, projectId));
            usage.CurrentUsage.Blocked += bucketBlocked?.Value ?? 0;
            usage.CurrentHourUsage.Blocked += bucketBlocked?.Value ?? 0;

            var bucketDiscarded = await _cache.GetAsync<int>(GetBucketDiscardedCacheKey(bucketUtc, organizationId, projectId));
            usage.CurrentUsage.Discarded += bucketDiscarded?.Value ?? 0;
            usage.CurrentHourUsage.Discarded += bucketDiscarded?.Value ?? 0;

            var bucketTooBig = await _cache.GetAsync<int>(GetBucketTooBigCacheKey(bucketUtc, organizationId, projectId));
            usage.CurrentUsage.TooBig += bucketTooBig?.Value ?? 0;
            usage.CurrentHourUsage.TooBig += bucketTooBig?.Value ?? 0;

            bucketUtc = bucketUtc.Add(_bucketSize);
        }

        return usage;
    }

    public async Task<int> GetEventsLeftAsync(string organizationId)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        (string OrganizationId, Organization? Organization) context = (OrganizationId: organizationId, Organization: null);
        int maxEventsPerMonth = await GetMaxEventsPerMonthAsync(context);

        // check for unlimited (-1) events
        if (maxEventsPerMonth < 0)
            return Int32.MaxValue;

        int currentTotal;
        var currentTotalCache = await _cache.GetAsync<int>(GetTotalCacheKey(utcNow, organizationId));
        if (currentTotalCache.HasValue)
        {
            currentTotal = currentTotalCache.Value;
        }
        else
        {
            if (context.Organization is null)
                context.Organization = await _organizationRepository.GetByIdAsync(organizationId, o => o.Cache());

            currentTotal = context.Organization.GetCurrentUsage(_timeProvider).Total;
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

        return Math.Max(eventsLeftInBucket, 0);
    }

    public async Task IncrementTotalAsync(string organizationId, string projectId, int eventCount = 1)
    {
        if (eventCount <= 0)
            return;

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        long bucketTotal = await _cache.IncrementAsync(GetBucketTotalCacheKey(utcNow, organizationId), eventCount, TimeSpan.FromHours(8));
        await _cache.IncrementAsync(GetBucketTotalCacheKey(utcNow, organizationId, projectId), eventCount, TimeSpan.FromHours(8));

        await _cache.ListAddAsync(GetOrganizationSetKey(utcNow), organizationId, TimeSpan.FromHours(8));
        await _cache.ListAddAsync(GetProjectSetKey(utcNow), projectId, TimeSpan.FromHours(8));

        int maxEventsPerMonth = await GetMaxEventsPerMonthAsync(organizationId);
        int bucketLimit = GetBucketEventLimit(maxEventsPerMonth);

        var currentTotalCache = await _cache.GetAsync<int>(GetTotalCacheKey(utcNow, organizationId));
        if (currentTotalCache.HasValue)
        {
            long monthTotal = currentTotalCache.Value + bucketTotal;
            if (monthTotal >= maxEventsPerMonth && monthTotal - maxEventsPerMonth < eventCount)
                await _messagePublisher.PublishAsync(new PlanOverage { OrganizationId = organizationId });
        }

        if (bucketTotal >= bucketLimit && bucketTotal - bucketLimit < eventCount)
        {
            // org will be throttled during the current bucket of time
            await _messagePublisher.PublishAsync(new PlanOverage { OrganizationId = organizationId, IsHourly = true });
            await _cache.SetAsync(GetThrottledKey(utcNow, organizationId), true, TimeSpan.FromMinutes(5));
        }
    }

    public async Task IncrementBlockedAsync(string organizationId, string? projectId, int eventCount = 1)
    {
        if (eventCount <= 0)
            return;

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        await _cache.IncrementAsync(GetBucketBlockedCacheKey(utcNow, organizationId), eventCount, TimeSpan.FromHours(8));
        await _cache.IncrementAsync(GetBucketBlockedCacheKey(utcNow, organizationId, projectId), eventCount, TimeSpan.FromHours(8));

        await _cache.ListAddAsync(GetOrganizationSetKey(utcNow), organizationId, TimeSpan.FromHours(8));
        await _cache.ListAddAsync(GetProjectSetKey(utcNow), projectId, TimeSpan.FromHours(8));

        AppDiagnostics.EventsBlocked.Add(eventCount);
    }

    public async Task IncrementDiscardedAsync(string organizationId, string projectId, int eventCount = 1)
    {
        if (eventCount <= 0)
            return;

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        await _cache.IncrementAsync(GetBucketDiscardedCacheKey(utcNow, organizationId), eventCount, TimeSpan.FromHours(8));
        await _cache.IncrementAsync(GetBucketDiscardedCacheKey(utcNow, organizationId, projectId), eventCount, TimeSpan.FromHours(8));

        await _cache.ListAddAsync(GetOrganizationSetKey(utcNow), organizationId, TimeSpan.FromHours(8));
        await _cache.ListAddAsync(GetProjectSetKey(utcNow), projectId, TimeSpan.FromHours(8));

        AppDiagnostics.EventsDiscarded.Add(eventCount);
    }

    public async Task IncrementTooBigAsync(string organizationId, string? projectId)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        await _cache.IncrementAsync(GetBucketTooBigCacheKey(utcNow, organizationId), 1, TimeSpan.FromHours(8));
        await _cache.IncrementAsync(GetBucketTooBigCacheKey(utcNow, organizationId, projectId), 1, TimeSpan.FromHours(8));

        await _cache.ListAddAsync(GetOrganizationSetKey(utcNow), organizationId, TimeSpan.FromHours(8));
        await _cache.ListAddAsync(GetProjectSetKey(utcNow), projectId, TimeSpan.FromHours(8));

        AppDiagnostics.PostTooBig.Add(1);
    }

    private int GetBucketEventLimit(int maxEventsPerMonth)
    {
        if (maxEventsPerMonth < 5000)
            return maxEventsPerMonth;

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var timeLeftInMonth = utcNow.EndOfMonth() - utcNow;
        if (timeLeftInMonth < TimeSpan.FromDays(1))
            return maxEventsPerMonth;

        double bucketsLeftInMonth = timeLeftInMonth / _bucketSize;

        // allow boosting to 10x the max / bucket if the events were divided evenly
        return (int)Math.Ceiling((maxEventsPerMonth / bucketsLeftInMonth) * 10);
    }

    private string GetTotalCacheKey(DateTime utcTime, string organizationId, string? projectId = null)
    {
        int bucket = GetTotalBucket(utcTime);

        if (String.IsNullOrEmpty(projectId))
            return $"usage:total:{bucket}:{organizationId}:total";

        return $"usage:total:{bucket}:{organizationId}:{projectId}:total";
    }

    private string GetBucketTotalCacheKey(DateTime utcTime, string organizationId, string? projectId = null)
    {
        int bucket = GetCurrentBucket(utcTime);

        if (String.IsNullOrEmpty(projectId))
            return $"usage:{bucket}:{organizationId}:total";

        return $"usage:{bucket}:{organizationId}:{projectId}:total";
    }

    private string GetBucketBlockedCacheKey(DateTime utcTime, string organizationId, string? projectId = null)
    {
        int bucket = GetCurrentBucket(utcTime);

        if (String.IsNullOrEmpty(projectId))
            return $"usage:{bucket}:{organizationId}:blocked";

        return $"usage:{bucket}:{organizationId}:{projectId}:blocked";
    }

    private string GetBucketDiscardedCacheKey(DateTime utcTime, string organizationId, string? projectId = null)
    {
        int bucket = GetCurrentBucket(utcTime);

        if (String.IsNullOrEmpty(projectId))
            return $"usage:{bucket}:{organizationId}:discarded";

        return $"usage:{bucket}:{organizationId}:{projectId}:discarded";
    }

    private string GetBucketTooBigCacheKey(DateTime utcTime, string organizationId, string? projectId = null)
    {
        int bucket = GetCurrentBucket(utcTime);

        if (String.IsNullOrEmpty(projectId))
            return $"usage:{bucket}:{organizationId}:toobig";

        return $"usage:{bucket}:{organizationId}:{projectId}:toobig";
    }

    private string GetOrganizationSetKey(DateTime utcTime)
    {
        int bucket = GetCurrentBucket(utcTime);
        return $"usage:{bucket}:organizations";
    }

    private string GetThrottledKey(DateTime utcTime, string organizationId)
    {
        int bucket = GetCurrentBucket(utcTime);
        return $"usage:{bucket}:{organizationId}:throttled";
    }

    private string GetProjectSetKey(DateTime utcTime)
    {
        int bucket = GetCurrentBucket(utcTime);
        return $"usage:{bucket}:projects";
    }

    private int GetCurrentBucket(DateTime utcTime) => utcTime.Floor(_bucketSize).ToEpoch();
    private int GetTotalBucket(DateTime utcTime) => utcTime.StartOfMonth().ToEpoch();
}
