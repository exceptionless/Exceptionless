using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

public partial class UsageService
{
    /// <summary>
    /// Calculates the complete organization, explicit project, and automatic smart-throttle allowance
    /// from the models already loaded by the ingestion job.
    /// </summary>
    public async Task<EventIngestAllowance> GetEventIngestAllowanceAsync(Organization organization, Project project)
    {
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(project);
        if (!String.Equals(project.OrganizationId, organization.Id, StringComparison.Ordinal))
            throw new ArgumentException("The project does not belong to the organization.", nameof(project));

        int maxEventsPerMonth = organization.GetMaxEventsPerMonthWithBonus(_timeProvider);
        int effectiveProjectLimit = GetEffectiveProjectLimit(project, maxEventsPerMonth);

        // This is the common self-hosted/unlimited path: no cache or repository reads are needed.
        if (maxEventsPerMonth < 0 && effectiveProjectLimit < 0)
            return EventIngestAllowance.Unlimited;

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var totals = await GetAcceptedUsageTotalsAsync(utcNow, organization, project);

        int organizationEventsLeft = GetOrganizationEventsLeft(maxEventsPerMonth, totals.OrganizationTotal, totals.OrganizationCurrentBucket);
        if (organizationEventsLeft <= 0)
        {
            return new EventIngestAllowance
            {
                EventsLeft = 0,
                IsOverOrgLimit = true,
                EffectiveProjectLimit = effectiveProjectLimit
            };
        }

        int projectEventsLeft = effectiveProjectLimit < 0
            ? Int32.MaxValue
            : Math.Max(0, effectiveProjectLimit - totals.ProjectTotal);
        if (projectEventsLeft <= 0)
        {
            return new EventIngestAllowance
            {
                EventsLeft = 0,
                IsOverProjectLimit = true,
                EffectiveProjectLimit = effectiveProjectLimit
            };
        }

        var smartThrottle = await GetSmartThrottleResultAsync(utcNow, organization, project, maxEventsPerMonth, totals);
        return new EventIngestAllowance
        {
            EventsLeft = Math.Min(organizationEventsLeft, projectEventsLeft),
            EffectiveProjectLimit = effectiveProjectLimit,
            SmartThrottle = smartThrottle
        };
    }

    public async Task<EventIngestAllowance> GetEventIngestAllowanceAsync(string organizationId, string projectId)
    {
        var organizationTask = _organizationRepository.GetByIdAsync(organizationId, o => o.Cache());
        var projectTask = _projectRepository.GetByIdAsync(projectId, o => o.Cache());
        await Task.WhenAll(organizationTask, projectTask);

        var organization = await organizationTask ?? throw new UsageServiceException($"Organization '{organizationId}' not found.");
        var project = await projectTask ?? throw new UsageServiceException($"Project '{projectId}' not found.");
        return await GetEventIngestAllowanceAsync(organization, project);
    }

    public async Task<SmartThrottleResult> GetSmartThrottleRateAsync(string organizationId, string projectId)
    {
        var organizationTask = _organizationRepository.GetByIdAsync(organizationId, o => o.Cache());
        var projectTask = _projectRepository.GetByIdAsync(projectId, o => o.Cache());
        await Task.WhenAll(organizationTask, projectTask);

        var organization = await organizationTask;
        var project = await projectTask;
        if (organization is null || project is null)
            return SmartThrottleResult.NoThrottle;

        return (await GetEventIngestAllowanceAsync(organization, project)).SmartThrottle;
    }

    public int GetEffectiveProjectLimit(Project project, Organization organization)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(organization);
        return GetEffectiveProjectLimit(project.IngestLimit, organization.GetMaxEventsPerMonthWithBonus(_timeProvider));
    }

    public int GetEffectiveProjectLimit(ProjectIngestLimit? ingestLimit, Organization organization)
    {
        ArgumentNullException.ThrowIfNull(organization);
        return GetEffectiveProjectLimit(ingestLimit, organization.GetMaxEventsPerMonthWithBonus(_timeProvider));
    }

    private static int GetEffectiveProjectLimit(Project project, int maxEventsPerMonth)
    {
        return GetEffectiveProjectLimit(project.IngestLimit, maxEventsPerMonth);
    }

    private static int GetEffectiveProjectLimit(ProjectIngestLimit? ingestLimit, int maxEventsPerMonth)
    {
        if (ingestLimit is null)
            return -1;

        if (ingestLimit.Type is ProjectIngestLimitType.Fixed)
        {
            if (ingestLimit.FixedLimit is not > 0)
                return -1;

            return maxEventsPerMonth < 0
                ? ingestLimit.FixedLimit.Value
                : Math.Min(ingestLimit.FixedLimit.Value, maxEventsPerMonth);
        }

        if (ingestLimit.Type is not ProjectIngestLimitType.PercentOfOrganizationLimit ||
            ingestLimit.PercentOfOrganizationLimit is not > 0 or > 100 ||
            maxEventsPerMonth < 0)
            return -1;

        return Math.Min(maxEventsPerMonth, (int)Math.Ceiling(maxEventsPerMonth * (double)ingestLimit.PercentOfOrganizationLimit.Value / 100));
    }

    private async Task<AcceptedUsageTotals> GetAcceptedUsageTotalsAsync(DateTime utcNow, Organization organization, Project project)
    {
        string organizationTotalKey = GetTotalCacheKey(utcNow, organization.Id);
        string projectTotalKey = GetTotalCacheKey(utcNow, organization.Id, project.Id);
        string organizationCurrentKey = GetBucketTotalCacheKey(utcNow, organization.Id);
        string projectCurrentKey = GetBucketTotalCacheKey(utcNow, organization.Id, project.Id);
        string organizationPreviousKey = GetBucketTotalCacheKey(utcNow.Subtract(_bucketSize), organization.Id);
        string projectPreviousKey = GetBucketTotalCacheKey(utcNow.Subtract(_bucketSize), organization.Id, project.Id);

        var values = await _cache.GetAllAsync<int>([
            organizationTotalKey,
            projectTotalKey,
            organizationCurrentKey,
            projectCurrentKey,
            organizationPreviousKey,
            projectPreviousKey
        ]);

        int organizationCurrent = GetCachedValue(values, organizationCurrentKey);
        int projectCurrent = GetCachedValue(values, projectCurrentKey);
        int organizationTotal = GetCachedValue(values, organizationTotalKey, organization.GetCurrentUsage(_timeProvider).Total)
            + organizationCurrent
            + GetCachedValue(values, organizationPreviousKey);
        int projectTotal = GetCachedValue(values, projectTotalKey, project.GetCurrentUsage(_timeProvider).Total)
            + projectCurrent
            + GetCachedValue(values, projectPreviousKey);

        return new AcceptedUsageTotals(organizationTotal, projectTotal, organizationCurrent, projectCurrent);
    }

    private static int GetCachedValue(IDictionary<string, CacheValue<int>> values, string key, int defaultValue = 0)
    {
        return values.TryGetValue(key, out var value) && value.HasValue ? value.Value : defaultValue;
    }

    private int GetOrganizationEventsLeft(int maxEventsPerMonth, int currentTotal, int currentBucketTotal)
    {
        if (maxEventsPerMonth < 0)
            return Int32.MaxValue;

        int monthlyEventsLeft = Math.Max(0, maxEventsPerMonth - currentTotal);
        int bucketEventsLeft = Math.Max(0, GetBucketEventLimit(maxEventsPerMonth) - currentBucketTotal);
        return Math.Min(monthlyEventsLeft, bucketEventsLeft);
    }

    private async Task<SmartThrottleResult> GetSmartThrottleResultAsync(DateTime utcNow, Organization organization, Project project, int maxEventsPerMonth, AcceptedUsageTotals totals)
    {
        if (!_appOptions.EnableSmartProjectThrottling || maxEventsPerMonth <= 0 || totals.OrganizationCurrentBucket <= 0 || totals.ProjectCurrentBucket <= 0)
            return SmartThrottleResult.NoThrottle;

        int remainingAllowance = Math.Max(0, maxEventsPerMonth - totals.OrganizationTotal);
        if (remainingAllowance <= 0)
            return SmartThrottleResult.NoThrottle;

        double windowsLeft = Math.Max(1, Math.Ceiling((utcNow.EndOfMonth() - utcNow).TotalMinutes / _bucketSize.TotalMinutes));
        double sustainableWindowAllowance = remainingAllowance / windowsLeft * 10;
        if (totals.OrganizationCurrentBucket < sustainableWindowAllowance * 0.8)
            return SmartThrottleResult.NoThrottle;

        int projectCount = (int)Math.Max(1, (await _projectRepository.GetCountByOrganizationIdAsync(organization.Id)).Total);
        if (projectCount <= 1)
            return SmartThrottleResult.NoThrottle;

        double fairShareWindowAllowance = sustainableWindowAllowance / projectCount;
        double fairShareRatio = fairShareWindowAllowance > 0 ? totals.ProjectCurrentBucket / fairShareWindowAllowance : Double.PositiveInfinity;
        if (fairShareRatio <= 2)
            return SmartThrottleResult.NoThrottle;

        var result = new SmartThrottleResult
        {
            IsThrottled = true,
            SampleRate = SmartThrottleResult.DefaultSampleRate,
            ProjectShare = (double)totals.ProjectTotal / Math.Max(1, totals.OrganizationTotal),
            FairShareRatio = fairShareRatio,
            CurrentProjectUsage = totals.ProjectTotal,
            FairShareLimit = maxEventsPerMonth / projectCount
        };

        await ActivateSmartThrottleAsync(utcNow, organization, project, result);
        return result;
    }

    private async Task ActivateSmartThrottleAsync(DateTime utcNow, Organization organization, Project project, SmartThrottleResult result)
    {
        string stateKey = GetProjectSmartThrottleKey(utcNow, organization.Id, project.Id);
        bool isNewTransition;
        try
        {
            isNewTransition = await _cache.AddAsync(stateKey, result, _bucketSize + TimeSpan.FromMinutes(1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record smart throttle state for project {ProjectId}", project.Id);
            return;
        }

        if (!isNewTransition)
            return;

        _logger.LogInformation("Smart project throttling activated for {OrganizationId}/{ProjectId} at {SampleRate:P0}; project usage {ProjectUsage}, fair-share limit {FairShareLimit}",
            organization.Id, project.Id, result.SampleRate, result.CurrentProjectUsage, result.FairShareLimit);

        string notificationKey = GetSmartThrottleNotificationKey(utcNow, organization.Id, project.Id);
        bool notificationClaimed = false;
        try
        {
            var notificationTtl = utcNow.EndOfMonth() - utcNow + TimeSpan.FromDays(1);
            notificationClaimed = await _cache.AddAsync(notificationKey, true, notificationTtl);
            if (!notificationClaimed)
                return;

            await _messagePublisher.PublishAsync(new ProjectSmartThrottleApplied
            {
                OrganizationId = organization.Id,
                ProjectId = project.Id,
                SampleRate = result.SampleRate,
                CurrentEventCount = result.CurrentProjectUsage,
                EventLimit = result.FairShareLimit
            });
        }
        catch (Exception ex)
        {
            if (notificationClaimed)
            {
                try
                {
                    await _cache.RemoveAsync(notificationKey);
                }
                catch (Exception cleanupException)
                {
                    _logger.LogWarning(cleanupException, "Failed to release smart throttle notification claim for project {ProjectId}", project.Id);
                }
            }

            _logger.LogError(ex, "Failed to publish smart throttle notification for project {ProjectId}", project.Id);
        }
    }

    public async Task<IReadOnlyDictionary<string, SmartThrottleResult>> GetProjectSmartThrottleStatesAsync(IReadOnlyCollection<(string OrganizationId, string ProjectId)> projects)
    {
        if (!_appOptions.EnableSmartProjectThrottling || projects.Count == 0)
            return new Dictionary<string, SmartThrottleResult>();

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var keysByProjectId = projects.ToDictionary(
            project => project.ProjectId,
            project => GetProjectSmartThrottleKey(utcNow, project.OrganizationId, project.ProjectId));
        var values = await _cache.GetAllAsync<SmartThrottleResult>(keysByProjectId.Values);

        return keysByProjectId
            .Where(pair => values.TryGetValue(pair.Value, out var value) && value.HasValue && value.Value.IsThrottled)
            .ToDictionary(pair => pair.Key, pair => values[pair.Value].Value);
    }

    public async Task<bool> IsProjectSmartThrottledAsync(string organizationId, string projectId)
    {
        if (!_appOptions.EnableSmartProjectThrottling)
            return false;

        var state = await _cache.GetAsync<SmartThrottleResult>(GetProjectSmartThrottleKey(_timeProvider.GetUtcNow().UtcDateTime, organizationId, projectId));
        return state is { HasValue: true, Value.IsThrottled: true };
    }

    public void RecordSmartThrottle(int blockedCount)
    {
        if (blockedCount > 0)
            AppDiagnostics.EventsSmartThrottled.Add(blockedCount);
    }

    public async Task EvaluateBudgetAlertsAfterSettingsChangeAsync(Organization organization, IReadOnlyCollection<int> thresholds)
    {
        if (organization.BudgetAlertSettings is not { Enabled: true } || thresholds.Count == 0)
            return;

        int maxEventsPerMonth = organization.GetMaxEventsPerMonthWithBonus(_timeProvider);
        if (maxEventsPerMonth <= 0)
            return;

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        string totalKey = GetTotalCacheKey(utcNow, organization.Id);
        string currentKey = GetBucketTotalCacheKey(utcNow, organization.Id);
        string previousKey = GetBucketTotalCacheKey(utcNow.Subtract(_bucketSize), organization.Id);
        var values = await _cache.GetAllAsync<int>([totalKey, currentKey, previousKey]);
        int currentTotal = GetCachedValue(values, totalKey, organization.GetCurrentUsage(_timeProvider).Total)
            + GetCachedValue(values, currentKey)
            + GetCachedValue(values, previousKey);

        await PublishCrossedBudgetAlertsAsync(organization, 0, currentTotal, maxEventsPerMonth, thresholds);
    }

    private async Task PublishCrossedBudgetAlertsAsync(Organization organization, int previousTotal, int currentTotal, int maxEventsPerMonth, IReadOnlyCollection<int>? thresholds = null)
    {
        if (organization.BudgetAlertSettings is not { Enabled: true, Thresholds: not null } || maxEventsPerMonth <= 0)
            return;

        thresholds ??= organization.BudgetAlertSettings.Thresholds;
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (int threshold in thresholds.Order())
        {
            if (threshold is <= 0 or >= 100)
                continue;

            int thresholdEventCount = (int)Math.Ceiling((double)threshold / 100 * maxEventsPerMonth);
            if (previousTotal >= thresholdEventCount || currentTotal < thresholdEventCount)
                continue;

            string alertSentKey = GetBudgetAlertSentKey(utcNow, organization.Id, threshold);
            bool alertClaimed = false;
            try
            {
                var alertTtl = utcNow.EndOfMonth() - utcNow + TimeSpan.FromDays(1);
                alertClaimed = await _cache.AddAsync(alertSentKey, true, alertTtl);
                if (!alertClaimed)
                    continue;

                await _messagePublisher.PublishAsync(new OrganizationBudgetAlert
                {
                    OrganizationId = organization.Id,
                    Threshold = threshold,
                    ThresholdEventCount = thresholdEventCount,
                    CurrentEventCount = currentTotal,
                    EventLimit = maxEventsPerMonth
                });
            }
            catch (Exception ex)
            {
                if (alertClaimed)
                {
                    try
                    {
                        await _cache.RemoveAsync(alertSentKey);
                    }
                    catch (Exception cleanupException)
                    {
                        _logger.LogWarning(cleanupException, "Failed to release budget alert {Threshold}% claim for organization {OrganizationId}", threshold, organization.Id);
                    }
                }

                _logger.LogError(ex, "Failed to publish budget alert {Threshold}% for organization {OrganizationId}", threshold, organization.Id);
            }
        }
    }

    private sealed record AcceptedUsageTotals(int OrganizationTotal, int ProjectTotal, int OrganizationCurrentBucket, int ProjectCurrentBucket);
}
