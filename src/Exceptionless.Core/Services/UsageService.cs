using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Lock;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

public class UsageService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICacheClient _cache;
    private readonly IIngestionQuotaStore _ingestionQuotaStore;
    private readonly IMessagePublisher _messagePublisher;
    private readonly NotificationService _notificationService;
    private readonly ILockProvider _lockProvider;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _idempotencyWindow;
    private readonly TimeSpan _requestTimeout;
    private readonly ILogger _logger;
    private readonly TimeSpan _bucketSize = TimeSpan.FromMinutes(5);

    public UsageService(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, ICacheClient cache, IIngestionQuotaStore ingestionQuotaStore, IMessagePublisher messagePublisher,
        NotificationService notificationService,
        ILockProvider lockProvider,
        AppOptions options,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _cache = cache;
        _ingestionQuotaStore = ingestionQuotaStore;
        _messagePublisher = messagePublisher;
        _notificationService = notificationService;
        _lockProvider = lockProvider;
        _idempotencyWindow = options.EventIngestionV3.IdempotencyWindow;
        _requestTimeout = options.EventIngestionV3.RequestTimeout;
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

        _logger.LogInformation("Saving organization usage starting from: {LastUsageSave}...", lastUsageSave);

        var bucketUtc = lastUsageSave;
        var currentBucketUtc = utcNow.Floor(_bucketSize);
        var processingCutoffUtc = currentBucketUtc.Subtract(_bucketSize);

        if (bucketUtc >= processingCutoffUtc)
            return;

        // do not process current bucket, should only be processing buckets whose window of time are complete
        while (bucketUtc < processingCutoffUtc)
        {
            DateTime pendingBucketUtc = bucketUtc;
            await DrainPendingUsageIdsAsync(GetOrganizationSetKey(pendingBucketUtc), async organizationId =>
            {
                await using var usageLock = await _lockProvider.TryAcquireAsync(
                    GetUsageBucketLockKey(pendingBucketUtc, organizationId),
                    TimeSpan.FromMinutes(1),
                    TimeSpan.FromSeconds(30));
                if (usageLock is null)
                    throw new InvalidOperationException($"Unable to acquire usage bucket lock for organization '{organizationId}'.");

                var organization = await _organizationRepository.GetByIdAsync(organizationId);
                if (organization is null)
                    return;

                _logger.LogInformation("Saving org ({OrganizationId}-{OrganizationName}) event usage for time bucket: {BucketUtc}...", organizationId, organization.Name, pendingBucketUtc);

                var processed = await _cache.GetAsync<bool>(GetV3BucketProcessedKey(pendingBucketUtc, organizationId));
                if (processed is { HasValue: true, Value: true })
                {
                    await CompleteOrganizationUsageBucketAsync(organization, pendingBucketUtc, utcNow);
                    return;
                }

                if (organization.LastAppliedUsageBucketUtc >= pendingBucketUtc)
                {
                    await _cache.SetAsync(GetV3BucketProcessedKey(pendingBucketUtc, organizationId), true, _idempotencyWindow);
                    await CompleteOrganizationUsageBucketAsync(organization, pendingBucketUtc, utcNow);
                    return;
                }

                int bucketTotal = await GetBucketTotalAsync(pendingBucketUtc, organizationId);
                var bucketBlocked = await _cache.GetAsync<int>(GetBucketBlockedCacheKey(pendingBucketUtc, organizationId));
                var bucketDiscarded = await _cache.GetAsync<int>(GetBucketDiscardedCacheKey(pendingBucketUtc, organizationId));
                var bucketTooBig = await _cache.GetAsync<int>(GetBucketTooBigCacheKey(pendingBucketUtc, organizationId));
                var bucketDeleted = await _cache.GetAsync<int>(GetBucketDeletedCacheKey(pendingBucketUtc, organizationId));

                bool hasIngestion = bucketTotal > 0 || (bucketBlocked?.Value ?? 0) > 0 || (bucketDiscarded?.Value ?? 0) > 0 || (bucketTooBig?.Value ?? 0) > 0;
                if (hasIngestion)
                    organization.LastEventDateUtc = _timeProvider.GetUtcNow().UtcDateTime;

                var usage = organization.GetUsage(pendingBucketUtc, _timeProvider);
                usage.Limit = organization.GetMaxEventsPerMonthWithBonus(_timeProvider);
                usage.Total += bucketTotal;
                usage.Blocked += bucketBlocked?.Value ?? 0;
                usage.Discarded += bucketDiscarded?.Value ?? 0;
                usage.TooBig += bucketTooBig?.Value ?? 0;
                usage.Deleted += bucketDeleted?.Value ?? 0;

                var hourlyUsage = organization.GetHourlyUsage(pendingBucketUtc);
                hourlyUsage.Total += bucketTotal;
                hourlyUsage.Blocked += bucketBlocked?.Value ?? 0;
                hourlyUsage.Discarded += bucketDiscarded?.Value ?? 0;
                hourlyUsage.TooBig += bucketTooBig?.Value ?? 0;
                hourlyUsage.Deleted += bucketDeleted?.Value ?? 0;

                organization.TrimUsage(_timeProvider);

                // Persist the authoritative marker with the totals. A retry after this save can
                // finish Redis acknowledgement and cleanup without applying the bucket twice.
                organization.LastAppliedUsageBucketUtc = pendingBucketUtc;
                await _organizationRepository.SaveAsync(organization);
                await _cache.SetAsync(GetV3BucketProcessedKey(pendingBucketUtc, organizationId), true, _idempotencyWindow);
                await CompleteOrganizationUsageBucketAsync(organization, pendingBucketUtc, utcNow);
            });

            await _cache.SetAsync("usage:last-organization-save", bucketUtc, TimeSpan.FromHours(8));

            bucketUtc = bucketUtc.Add(_bucketSize);
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

        _logger.LogInformation("Saving project usage starting from: {LastUsageSave}...", lastUsageSave);

        var bucketUtc = lastUsageSave;
        var currentBucketUtc = utcNow.Floor(_bucketSize);
        var processingCutoffUtc = currentBucketUtc.Subtract(_bucketSize);

        if (bucketUtc >= processingCutoffUtc)
            return;

        // do not process current bucket, should only be processing buckets whose window of time are complete
        while (bucketUtc < processingCutoffUtc)
        {
            DateTime pendingBucketUtc = bucketUtc;
            await DrainPendingUsageIdsAsync(GetProjectSetKey(pendingBucketUtc), async projectId =>
            {
                var project = await _projectRepository.GetByIdAsync(projectId);
                if (project is null)
                    return;

                await using var usageLock = await _lockProvider.TryAcquireAsync(
                    GetUsageBucketLockKey(pendingBucketUtc, project.OrganizationId, projectId),
                    TimeSpan.FromMinutes(1),
                    TimeSpan.FromSeconds(30));
                if (usageLock is null)
                    throw new InvalidOperationException($"Unable to acquire usage bucket lock for project '{projectId}'.");

                // The project may have been saved while this worker waited for the lock.
                project = await _projectRepository.GetByIdAsync(projectId);
                if (project is null)
                    return;

                _logger.LogInformation("Saving project ({ProjectId}-{ProjectName}) event usage for time bucket: {BucketUtc}...", projectId, project.Name, pendingBucketUtc);

                var processed = await _cache.GetAsync<bool>(GetV3BucketProcessedKey(pendingBucketUtc, project.OrganizationId, projectId));
                if (processed is { HasValue: true, Value: true })
                {
                    await CompleteProjectUsageBucketAsync(project, pendingBucketUtc, utcNow);
                    return;
                }

                if (project.LastAppliedUsageBucketUtc >= pendingBucketUtc)
                {
                    await _cache.SetAsync(GetV3BucketProcessedKey(pendingBucketUtc, project.OrganizationId, projectId), true, _idempotencyWindow);
                    await CompleteProjectUsageBucketAsync(project, pendingBucketUtc, utcNow);
                    return;
                }

                int bucketTotal = await GetBucketTotalAsync(pendingBucketUtc, project.OrganizationId, projectId);
                var bucketBlocked = await _cache.GetAsync<int>(GetBucketBlockedCacheKey(pendingBucketUtc, project.OrganizationId, projectId));
                var bucketDiscarded = await _cache.GetAsync<int>(GetBucketDiscardedCacheKey(pendingBucketUtc, project.OrganizationId, projectId));
                var bucketTooBig = await _cache.GetAsync<int>(GetBucketTooBigCacheKey(pendingBucketUtc, project.OrganizationId, projectId));
                var bucketDeleted = await _cache.GetAsync<int>(GetBucketDeletedCacheKey(pendingBucketUtc, project.OrganizationId, projectId));

                bool hasIngestion = bucketTotal > 0 || (bucketBlocked?.Value ?? 0) > 0 || (bucketDiscarded?.Value ?? 0) > 0 || (bucketTooBig?.Value ?? 0) > 0;
                if (hasIngestion)
                    project.LastEventDateUtc = _timeProvider.GetUtcNow().UtcDateTime;

                (string OrganizationId, Organization? Organization) context = (OrganizationId: project.OrganizationId, Organization: null);
                int maxEventsPerMonth = await GetMaxEventsPerMonthAsync(context);

                var usage = project.GetUsage(pendingBucketUtc);
                usage.Limit = maxEventsPerMonth;
                usage.Total += bucketTotal;
                usage.Blocked += bucketBlocked?.Value ?? 0;
                usage.Discarded += bucketDiscarded?.Value ?? 0;
                usage.TooBig += bucketTooBig?.Value ?? 0;
                usage.Deleted += bucketDeleted?.Value ?? 0;

                var hourlyUsage = project.GetHourlyUsage(pendingBucketUtc);
                hourlyUsage.Total += bucketTotal;
                hourlyUsage.Blocked += bucketBlocked?.Value ?? 0;
                hourlyUsage.Discarded += bucketDiscarded?.Value ?? 0;
                hourlyUsage.TooBig += bucketTooBig?.Value ?? 0;
                hourlyUsage.Deleted += bucketDeleted?.Value ?? 0;

                project.TrimUsage(_timeProvider);

                project.LastAppliedUsageBucketUtc = pendingBucketUtc;
                await _projectRepository.SaveAsync(project);
                await _cache.SetAsync(GetV3BucketProcessedKey(pendingBucketUtc, project.OrganizationId, projectId), true, _idempotencyWindow);
                await CompleteProjectUsageBucketAsync(project, pendingBucketUtc, utcNow);
            });

            await _cache.SetAsync("usage:last-project-save", bucketUtc, TimeSpan.FromHours(8));

            bucketUtc = bucketUtc.Add(_bucketSize);
        }
    }

    private async Task DrainPendingUsageIdsAsync(string discoveryKey, Func<string, Task> saveAsync)
    {
        while (true)
        {
            var pendingIds = await _cache.GetListAsync<string>(discoveryKey);
            if (!pendingIds.HasValue || pendingIds.Value.Count == 0)
                return;

            foreach (string pendingId in pendingIds.Value.Distinct(StringComparer.Ordinal))
            {
                if (!String.IsNullOrEmpty(pendingId))
                    await saveAsync(pendingId);

                // A discovery id is acknowledged only after its durable save, processed marker,
                // and cache cleanup all succeed. Failed and not-yet-visited ids remain for retry.
                await _cache.ListRemoveAsync(discoveryKey, [pendingId]);
            }
        }
    }

    private Task CompleteOrganizationUsageBucketAsync(Organization organization, DateTime bucketUtc, DateTime utcNow)
    {
        var usage = organization.GetUsage(bucketUtc, _timeProvider);
        return Task.WhenAll(
            _cache.RemoveAllAsync(new[] {
                GetBucketTotalCacheKey(bucketUtc, organization.Id),
                GetBucketBlockedCacheKey(bucketUtc, organization.Id),
                GetBucketDiscardedCacheKey(bucketUtc, organization.Id),
                GetBucketTooBigCacheKey(bucketUtc, organization.Id),
                GetBucketDeletedCacheKey(bucketUtc, organization.Id),
                GetThrottledKey(bucketUtc, organization.Id)
            }),
            _cache.SetAsync(GetTotalCacheKey(utcNow, organization.Id), usage.Total, TimeSpan.FromHours(8)));
    }

    private Task CompleteProjectUsageBucketAsync(Project project, DateTime bucketUtc, DateTime utcNow)
    {
        var usage = project.GetUsage(bucketUtc);
        return Task.WhenAll(
            _cache.RemoveAllAsync(new[] {
                GetBucketTotalCacheKey(bucketUtc, project.OrganizationId, project.Id),
                GetBucketDiscardedCacheKey(bucketUtc, project.OrganizationId, project.Id),
                GetBucketBlockedCacheKey(bucketUtc, project.OrganizationId, project.Id),
                GetBucketTooBigCacheKey(bucketUtc, project.OrganizationId, project.Id),
                GetBucketDeletedCacheKey(bucketUtc, project.OrganizationId, project.Id)
            }),
            _cache.SetAsync(GetTotalCacheKey(utcNow, project.OrganizationId, project.Id), usage.Total, TimeSpan.FromHours(8)));
    }

    public async Task HandleOrganizationChangeAsync(Organization modified, Organization original)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        await _cache.RemoveAsync($"usage:limits:{modified.Id}");

        int modifiedMaxEvents = modified.GetMaxEventsPerMonthWithBonus(_timeProvider);
        int originalMaxEvents = original.GetMaxEventsPerMonthWithBonus(_timeProvider);
        if (modifiedMaxEvents == originalMaxEvents)
            return;

        bool isMonthlyLimitIncrease = modifiedMaxEvents < 0 || (originalMaxEvents >= 0 && modifiedMaxEvents > originalMaxEvents);
        if (isMonthlyLimitIncrease)
        {
            // A higher monthly limit only resets monthly notification state when it actually ends the current overage.
            if (!modified.IsOverMonthlyLimit(_timeProvider))
                await _notificationService.RemoveOrganizationNotificationSentAsync(modified.Id, isOverMonthlyLimit: true);

            await _cache.RemoveAsync(GetThrottledKey(utcNow, modified.Id));
            return;
        }

        bool wasOverMonthlyLimit = original.IsOverMonthlyLimit(_timeProvider);
        bool isOverMonthlyLimit = modified.IsOverMonthlyLimit(_timeProvider);
        if (!wasOverMonthlyLimit && isOverMonthlyLimit)
        {
            await _notificationService.RemoveOrganizationNotificationSentAsync(modified.Id, isOverMonthlyLimit: true);
            await _messagePublisher.PublishAsync(new PlanOverage { OrganizationId = modified.Id });
        }

        int bucketTotal = await GetBucketTotalAsync(utcNow, modified.Id);
        if (bucketTotal == 0)
            return;

        int bucketLimit = GetBucketEventLimit(modifiedMaxEvents);

        // unlimited
        if (bucketLimit < 0)
            return;

        if (bucketTotal >= bucketLimit)
        {
            await _messagePublisher.PublishAsync(new PlanOverage { OrganizationId = modified.Id, IsHourly = true });
            await _cache.SetAsync(GetThrottledKey(utcNow, modified.Id), true, TimeSpan.FromMinutes(5));
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
            if (organization is null)
                throw new UsageServiceException($"Organization '{organizationId}' not found.");

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
            if (project is null)
                throw new UsageServiceException($"Project '{projectId}' not found.");

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
            int bucketTotal = await GetBucketTotalAsync(bucketUtc, organizationId, projectId);
            usage.CurrentUsage.Total += bucketTotal;
            usage.CurrentHourUsage.Total += bucketTotal;

            var bucketBlocked = await _cache.GetAsync<int>(GetBucketBlockedCacheKey(bucketUtc, organizationId, projectId));
            usage.CurrentUsage.Blocked += bucketBlocked?.Value ?? 0;
            usage.CurrentHourUsage.Blocked += bucketBlocked?.Value ?? 0;

            var bucketDiscarded = await _cache.GetAsync<int>(GetBucketDiscardedCacheKey(bucketUtc, organizationId, projectId));
            usage.CurrentUsage.Discarded += bucketDiscarded?.Value ?? 0;
            usage.CurrentHourUsage.Discarded += bucketDiscarded?.Value ?? 0;

            var bucketTooBig = await _cache.GetAsync<int>(GetBucketTooBigCacheKey(bucketUtc, organizationId, projectId));
            usage.CurrentUsage.TooBig += bucketTooBig?.Value ?? 0;
            usage.CurrentHourUsage.TooBig += bucketTooBig?.Value ?? 0;

            var bucketDeleted = await _cache.GetAsync<int>(GetBucketDeletedCacheKey(bucketUtc, organizationId, projectId));
            usage.CurrentUsage.Deleted += bucketDeleted?.Value ?? 0;
            usage.CurrentHourUsage.Deleted += bucketDeleted?.Value ?? 0;

            bucketUtc = bucketUtc.Add(_bucketSize);
        }

        return usage;
    }

    public async Task<int> GetEventsLeftAsync(string organizationId)
    {
        int maxEventsPerMonth = await GetMaxEventsPerMonthAsync(organizationId);
        return await GetEventsLeftAsync(organizationId, maxEventsPerMonth);
    }

    private async Task<int> GetEventsLeftAsync(string organizationId, int maxEventsPerMonth)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        (string OrganizationId, Organization? Organization) context = (OrganizationId: organizationId, Organization: null);

        // check for unlimited (-1) events
        if (maxEventsPerMonth < 0)
            return Int32.MaxValue;

        // These values are independent Redis reads. Issue them together because this method runs
        // under the short quota-decision lock for V3 ingestion.
        var currentTotalCacheTask = _cache.GetAsync<int>(GetTotalCacheKey(utcNow, organizationId));
        var bucketTotalTask = GetBucketTotalAsync(utcNow, organizationId);
        var previousBucketTotalTask = GetBucketTotalAsync(utcNow.Subtract(_bucketSize), organizationId);
        await Task.WhenAll(currentTotalCacheTask, bucketTotalTask, previousBucketTotalTask);

        int currentTotal;
        var currentTotalCache = await currentTotalCacheTask;
        if (currentTotalCache.HasValue)
        {
            currentTotal = currentTotalCache.Value;
        }
        else
        {
            if (context.Organization is null)
                context.Organization = await _organizationRepository.GetByIdAsync(organizationId, o => o.Cache());

            if (context.Organization is null)
                throw new UsageServiceException($"Organization '{organizationId}' not found.");

            currentTotal = context.Organization.GetCurrentUsage(_timeProvider).Total;
            await _cache.SetAsync(GetTotalCacheKey(utcNow, organizationId), currentTotal, TimeSpan.FromHours(8));
        }

        // if already over limit, return
        if (currentTotal >= maxEventsPerMonth)
            return 0;

        // get current bucket counter and add it to total
        int bucketTotal = await bucketTotalTask;
        currentTotal += bucketTotal;

        // get previous bucket counter and add it to total since it might not be saved yet
        int previousBucketTotal = await previousBucketTotalTask;
        currentTotal += previousBucketTotal;

        // check to see if adding this bucket puts the org over the limit
        if (currentTotal >= maxEventsPerMonth)
            return 0;

        // get a bucket level limit to help spread the events out more evenly (allows bursting)
        int bucketLimit = GetBucketEventLimit(maxEventsPerMonth);
        int eventsLeftInBucket = bucketLimit - bucketTotal;
        int eventsLeftInMonth = maxEventsPerMonth - currentTotal;

        return Math.Max(Math.Min(eventsLeftInBucket, eventsLeftInMonth), 0);
    }

    public async Task<EventIngestionReservation> ReserveEventsAsync(string organizationId, int eventCount)
    {
        string reservationId = Guid.NewGuid().ToString("N");
        if (eventCount <= 0)
            return new EventIngestionReservation(reservationId, organizationId, 0);

        // Availability and the Redis lease must be one serialized decision. Otherwise a delayed
        // caller can retain an old availability snapshot until a prior request has committed and
        // released its lease, then reuse that capacity. The lock is scoped per organization, so
        // unrelated tenants and scale-out replicas continue independently.
        await using var quotaLock = await _lockProvider.TryAcquireAsync(
            String.Concat("usage:quota-reservation:", organizationId),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(30));
        if (quotaLock is null)
            throw new InvalidOperationException($"Unable to acquire the ingestion quota lock for organization '{organizationId}'.");

        int maxEventsPerMonth = await GetMaxEventsPerMonthAsync(organizationId);
        if (maxEventsPerMonth < 0)
            return EventIngestionReservation.Unlimited(organizationId, eventCount);
        int eventsLeft = await GetEventsLeftAsync(organizationId, maxEventsPerMonth);
        int admittedCount = await _ingestionQuotaStore.ReserveAsync(
            organizationId,
            reservationId,
            eventCount,
            eventsLeft,
            TimeSpan.FromMinutes(10));
        return new EventIngestionReservation(reservationId, organizationId, admittedCount);
    }

    public async Task ReleaseEventReservationAsync(EventIngestionReservation reservation)
    {
        if (reservation.Count <= 0 || reservation.IsUnlimited)
            return;

        await _ingestionQuotaStore.ReleaseAsync(reservation.OrganizationId, reservation.Id);
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

    public async Task IncrementTotalAsync(string organizationId, string projectId, IReadOnlyCollection<EventUsageSettlement> settlements)
    {
        if (settlements.Count == 0)
            return;

        // Only the writer that successfully creates a durable event may submit a settlement.
        // Keep a final age guard so delayed recovery cannot reconstruct a closed bucket. This
        // intentionally fails open for billing rather than keeping a per-event Redis ledger.
        DateTime settlementCutoffUtc = _timeProvider.GetUtcNow().UtcDateTime.Subtract(_idempotencyWindow);
        foreach (var bucket in settlements
            .Where(settlement => settlement.CreatedUtc > settlementCutoffUtc)
            .DistinctBy(settlement => settlement.EventId, StringComparer.Ordinal)
            .GroupBy(settlement => settlement.CreatedUtc.Floor(_bucketSize)))
        {
            EventUsageSettlement[] batchSettlements = bucket.ToArray();
            DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            DateTime currentBucketUtc = nowUtc.Floor(_bucketSize);
            bool isCurrentBucket = bucket.Key == currentBucketUtc;
            bool isActivePreviousBucket = bucket.Key == currentBucketUtc.Subtract(_bucketSize)
                && batchSettlements.All(settlement => nowUtc.Subtract(settlement.CreatedUtc) <= _requestTimeout);
            int eventCount = batchSettlements.Length;
            if (isCurrentBucket || isActivePreviousBucket)
            {
                // Current and previous buckets have a full grace window before the saver can
                // close them. Keep this hot path lock-free and O(1): register discovery before
                // incrementing scalar counters so a crash can undercount but cannot orphan usage.
                await Task.WhenAll(
                    _cache.ListAddAsync(GetOrganizationSetKey(bucket.Key), organizationId, TimeSpan.FromHours(8)),
                    _cache.ListAddAsync(GetProjectSetKey(bucket.Key), projectId, TimeSpan.FromHours(8)));
                long[] updatedTotals = await Task.WhenAll(
                    _cache.IncrementAsync(GetBucketTotalCacheKey(bucket.Key, organizationId), eventCount, TimeSpan.FromHours(8)),
                    _cache.IncrementAsync(GetBucketTotalCacheKey(bucket.Key, organizationId, projectId), eventCount, TimeSpan.FromHours(8)));
                await HandleV3UsageAddedAsync(organizationId, eventCount, bucket.Key, updatedTotals[0]);
                continue;
            }

            string[] usageLockKeys =
            [
                GetUsageBucketLockKey(bucket.Key, organizationId),
                GetUsageBucketLockKey(bucket.Key, organizationId, projectId)
            ];
            await using var usageLock = await _lockProvider.TryAcquireAsync(
                usageLockKeys,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromSeconds(30));
            if (usageLock is null)
                throw new InvalidOperationException($"Unable to acquire usage bucket locks for project '{projectId}'.");

            var organizationProcessedTask = _cache.GetAsync<bool>(GetV3BucketProcessedKey(bucket.Key, organizationId));
            var projectProcessedTask = _cache.GetAsync<bool>(GetV3BucketProcessedKey(bucket.Key, organizationId, projectId));
            await Task.WhenAll(organizationProcessedTask, projectProcessedTask);
            var organizationProcessed = await organizationProcessedTask;
            var projectProcessed = await projectProcessedTask;
            bool isOrganizationProcessed = organizationProcessed.HasValue && organizationProcessed.Value;
            bool isProjectProcessed = projectProcessed.HasValue && projectProcessed.Value;
            DateTime organizationBucketUtc = isOrganizationProcessed ? currentBucketUtc : bucket.Key;
            DateTime projectBucketUtc = isProjectProcessed ? currentBucketUtc : bucket.Key;

            await Task.WhenAll(
                _cache.ListAddAsync(GetOrganizationSetKey(organizationBucketUtc), organizationId, TimeSpan.FromHours(8)),
                _cache.ListAddAsync(GetProjectSetKey(projectBucketUtc), projectId, TimeSpan.FromHours(8)));
            long[] totals = await Task.WhenAll(
                _cache.IncrementAsync(GetBucketTotalCacheKey(organizationBucketUtc, organizationId), eventCount, TimeSpan.FromHours(8)),
                _cache.IncrementAsync(GetBucketTotalCacheKey(projectBucketUtc, organizationId, projectId), eventCount, TimeSpan.FromHours(8)));

            await HandleV3UsageAddedAsync(organizationId, eventCount, organizationBucketUtc, totals[0]);
        }
    }

    private async Task HandleV3UsageAddedAsync(string organizationId, int eventCount, DateTime bucketUtc, long bucketTotal)
    {
        int maxEventsPerMonth = await GetMaxEventsPerMonthAsync(organizationId);
        if (maxEventsPerMonth < 0)
            return;

        int bucketLimit = GetBucketEventLimit(maxEventsPerMonth);
        var currentTotalCache = await _cache.GetAsync<int>(GetTotalCacheKey(bucketUtc, organizationId));
        if (currentTotalCache.HasValue)
        {
            long monthTotal = currentTotalCache.Value + bucketTotal;
            if (monthTotal >= maxEventsPerMonth && monthTotal - maxEventsPerMonth < eventCount)
                await _messagePublisher.PublishAsync(new PlanOverage { OrganizationId = organizationId });
        }

        if (bucketTotal >= bucketLimit && bucketTotal - bucketLimit < eventCount)
        {
            await _messagePublisher.PublishAsync(new PlanOverage { OrganizationId = organizationId, IsHourly = true });
            await _cache.SetAsync(GetThrottledKey(bucketUtc, organizationId), true, TimeSpan.FromMinutes(5));
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
        if (projectId is not null)
            await _cache.ListAddAsync(GetProjectSetKey(utcNow), projectId, TimeSpan.FromHours(8));

        AppDiagnostics.EventsBlocked.Add(eventCount);
    }

    // projectId is intentionally non-nullable: discarded events are only counted after project resolution
    // (unlike Blocked/TooBig which can occur before a project is identified).
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
        if (projectId is not null)
            await _cache.ListAddAsync(GetProjectSetKey(utcNow), projectId, TimeSpan.FromHours(8));

        AppDiagnostics.PostTooBig.Add(1);
    }

    public async Task IncrementDeletedAsync(string organizationId, string? projectId, int eventCount = 1)
    {
        if (eventCount <= 0)
            return;

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var tasks = new List<Task>(4)
        {
            _cache.IncrementAsync(GetBucketDeletedCacheKey(utcNow, organizationId), eventCount, TimeSpan.FromHours(8)),
            _cache.ListAddAsync(GetOrganizationSetKey(utcNow), organizationId, TimeSpan.FromHours(8))
        };

        if (!String.IsNullOrEmpty(projectId))
        {
            tasks.Add(_cache.IncrementAsync(GetBucketDeletedCacheKey(utcNow, organizationId, projectId), eventCount, TimeSpan.FromHours(8)));
            tasks.Add(_cache.ListAddAsync(GetProjectSetKey(utcNow), projectId, TimeSpan.FromHours(8)));
        }

        await Task.WhenAll(tasks);
        AppDiagnostics.EventsDeleted.Add(eventCount);
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

    private async Task<int> GetBucketTotalAsync(DateTime utcTime, string organizationId, string? projectId = null)
    {
        var total = await _cache.GetAsync<int>(GetBucketTotalCacheKey(utcTime, organizationId, projectId));
        return total.HasValue ? total.Value : 0;
    }

    private string GetV3BucketProcessedKey(DateTime utcTime, string organizationId, string? projectId = null)
    {
        int bucket = GetCurrentBucket(utcTime);
        return String.IsNullOrEmpty(projectId)
            ? $"usage:{bucket}:{organizationId}:total:v3:processed"
            : $"usage:{bucket}:{organizationId}:{projectId}:total:v3:processed";
    }

    private string GetUsageBucketLockKey(DateTime utcTime, string organizationId, string? projectId = null)
    {
        int bucket = GetCurrentBucket(utcTime);
        return String.IsNullOrEmpty(projectId)
            ? $"usage:{bucket}:{organizationId}:v3:lock"
            : $"usage:{bucket}:{organizationId}:{projectId}:v3:lock";
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

    private string GetBucketDeletedCacheKey(DateTime utcTime, string organizationId, string? projectId = null)
    {
        int bucket = GetCurrentBucket(utcTime);

        if (String.IsNullOrEmpty(projectId))
            return $"usage:{bucket}:{organizationId}:deleted";

        return $"usage:{bucket}:{organizationId}:{projectId}:deleted";
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
