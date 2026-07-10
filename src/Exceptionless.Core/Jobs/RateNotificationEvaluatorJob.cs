using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Resilience;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs;

[Job(Description = "Evaluates rate notification rules and enqueues notifications.", IsContinuous = false)]
public class RateNotificationEvaluatorJob : JobWithLockBase
{
    private readonly RateCounterService _counterService;
    private readonly IRateNotificationRuleRepository _ruleRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IQueue<RateNotification> _notificationQueue;
    private readonly ILockProvider _lockProvider;

    /// <summary>How far back to scan for active counter minutes.</summary>
    private static readonly TimeSpan ScanWindow = TimeSpan.FromHours(2);

    public RateNotificationEvaluatorJob(
        RateCounterService counterService,
        IRateNotificationRuleRepository ruleRepository,
        IOrganizationRepository organizationRepository,
        IQueue<RateNotification> notificationQueue,
        ILockProvider lockProvider,
        TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider,
        ILoggerFactory loggerFactory) : base(timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        _counterService = counterService;
        _ruleRepository = ruleRepository;
        _organizationRepository = organizationRepository;
        _notificationQueue = notificationQueue;
        _lockProvider = lockProvider;
    }

    protected override Task<ILock?> GetLockAsync(CancellationToken cancellationToken = default)
    {
        return _lockProvider.TryAcquireAsync(nameof(RateNotificationEvaluatorJob), TimeSpan.FromMinutes(2), cancellationToken);
    }

    protected override async Task<JobResult> RunInternalAsync(JobContext context)
    {
        using var evaluationTimer = AppDiagnostics.RateNotificationEvaluationTime.StartTimer();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
        var toMinute = currentMinute.AddMinutes(-1);
        var lastEvaluatedMinute = await _counterService.GetLastEvaluatedMinuteAsync(context.CancellationToken);
        var earliestRecoveryMinute = toMinute.Subtract(ScanWindow).AddMinutes(1);
        var fromMinute = lastEvaluatedMinute.HasValue
            ? new[] { lastEvaluatedMinute.Value.AddMinutes(1), earliestRecoveryMinute }.Max()
            : earliestRecoveryMinute;

        if (fromMinute > toMinute)
            return JobResult.Success;

        _logger.LogInformation("Evaluating rate notification rules from {From} to {To}", fromMinute, toMinute);

        // Collect all unique counter keys across all minutes in the scan window
        var allCounterKeys = new HashSet<string>();
        for (var minute = fromMinute; minute <= toMinute; minute = minute.AddMinutes(1))
        {
            var keys = await _counterService.GetActiveCounterKeysAsync(minute, context.CancellationToken);
            foreach (var key in keys)
                allCounterKeys.Add(key);
        }

        var counterKeysByProject = allCounterKeys
            .Select(counterKey => (CounterKey: counterKey, ProjectId: ParseProjectIdFromCounterKey(counterKey)))
            .Where(entry => entry.ProjectId is not null)
            .GroupBy(entry => entry.ProjectId!, entry => entry.CounterKey)
            .ToList();

        AppDiagnostics.RateNotificationActiveCounterKeys.Record(allCounterKeys.Count);
        AppDiagnostics.RateNotificationActiveProjects.Record(counterKeysByProject.Count);

        foreach (var projectCounterKeys in counterKeysByProject)
        {
            if (context.CancellationToken.IsCancellationRequested)
                return JobResult.Cancelled;

            await EvaluateProjectAsync(projectCounterKeys.Key, projectCounterKeys, currentMinute, context.CancellationToken);
        }

        await _counterService.SetLastEvaluatedMinuteAsync(toMinute, context.CancellationToken);
        _logger.LogInformation("Finished evaluating rate notification rules");
        return JobResult.Success;
    }

    private async Task EvaluateProjectAsync(string projectId, IEnumerable<string> counterKeys, DateTime evaluationEndUtc, CancellationToken ct)
    {
        var ruleResults = await _ruleRepository.GetEnabledByProjectIdAsync(projectId, o => o.SearchAfterPaging().PageLimit(1000));
        var allProjectRules = new List<RateNotificationRule>();
        while (ruleResults.Documents.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            allProjectRules.AddRange(ruleResults.Documents);
            if (!await ruleResults.NextPageAsync())
                break;
        }

        if (allProjectRules.Count == 0)
            return;

        string organizationId = allProjectRules[0].OrganizationId;
        var organization = await _organizationRepository.GetByIdAsync(organizationId, o => o.Cache());
        if (organization is null || !organization.HasRateNotifications())
            return;

        var rulesByCounterKey = allProjectRules
            .GroupBy(UpdateRateCountersAction.BuildCounterKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        foreach (string counterKey in counterKeys)
        {
            if (!rulesByCounterKey.TryGetValue(counterKey, out var matchingRules))
                continue;

            foreach (var rule in matchingRules)
            {
                if (ct.IsCancellationRequested)
                    return;

                await EvaluateRuleAsync(rule, counterKey, evaluationEndUtc, ct);
            }
        }
    }

    private async Task EvaluateRuleAsync(RateNotificationRule rule, string counterKey, DateTime evaluationEndUtc, CancellationToken ct)
    {
        if (!rule.IsEnabled)
            return;

        // Skip if actively snoozed
        if (rule.SnoozedUntilUtc.HasValue && rule.SnoozedUntilUtc.Value > evaluationEndUtc)
        {
            _logger.LogDebug("Skipping snoozed rule {RuleId} snoozed until {SnoozedUntil}", rule.Id, rule.SnoozedUntilUtc);
            return;
        }

        var windowStartUtc = evaluationEndUtc.Subtract(rule.Window);

        // SNOOZE BACK-ALERT FIX:
        // If the rule was recently un-snoozed (SnoozedUntilUtc is set and in the past), use that as the
        // effective window start to ignore traffic that occurred during the snooze period.
        var snoozeBoundaryUtc = rule.SnoozedUntilUtc?.Ceiling(TimeSpan.FromMinutes(1));
        var effectiveWindowStartUtc = snoozeBoundaryUtc.HasValue && snoozeBoundaryUtc.Value > windowStartUtc
            ? snoozeBoundaryUtc.Value
            : windowStartUtc;

        var observedCount = await _counterService.SumBucketsAsync(counterKey, effectiveWindowStartUtc, evaluationEndUtc, ct);

        if (observedCount < rule.Threshold)
        {
            _logger.LogDebug("Rule {RuleId}: observed={Observed} < threshold={Threshold}, skipping", rule.Id, observedCount, rule.Threshold);
            return;
        }

        // Build subject key (for cooldown scoping)
        string subjectKey = BuildSubjectKey(rule);

        // Atomically claim the cooldown before enqueueing so overlapping evaluators cannot duplicate alerts.
        if (!await _counterService.TrySetCooldownAsync(rule.Id, subjectKey, rule.Cooldown, ct))
        {
            _logger.LogDebug("Rule {RuleId} is on cooldown for subject {SubjectKey}", rule.Id, subjectKey);
            return;
        }

        try
        {
            await _notificationQueue.EnqueueAsync(new RateNotification
            {
                RuleId = rule.Id,
                RuleVersion = rule.Version,
                OrganizationId = rule.OrganizationId,
                ProjectId = rule.ProjectId,
                UserId = rule.UserId,
                SubjectKey = subjectKey,
                StackId = rule.StackId,
                WindowStartUtc = effectiveWindowStartUtc,
                WindowEndUtc = evaluationEndUtc,
                ObservedCount = observedCount,
                Threshold = rule.Threshold
            });
            AppDiagnostics.RateNotificationsEnqueued.Add(1);
        }
        catch
        {
            await _counterService.RemoveCooldownAsync(rule.Id, subjectKey, ct);
            throw;
        }

        // Update LastFiredUtc
        rule.LastFiredUtc = evaluationEndUtc;
        await _ruleRepository.SaveAsync(rule);

        _logger.LogInformation("Rate notification fired: rule={RuleId} project={ProjectId} observed={Observed} threshold={Threshold}",
            rule.Id, rule.ProjectId, observedCount, rule.Threshold);
    }

    /// <summary>Parses the projectId from a counter key of the form: project:{projectId}:... </summary>
    private static string? ParseProjectIdFromCounterKey(string counterKey)
    {
        const string prefix = "project:";
        if (!counterKey.StartsWith(prefix))
            return null;

        int start = prefix.Length;
        int end = counterKey.IndexOf(':', start);
        if (end < 0)
            return null;

        return counterKey[start..end];
    }

    private static string BuildSubjectKey(RateNotificationRule rule)
    {
        return rule.Subject == RateNotificationSubject.Stack && !String.IsNullOrEmpty(rule.StackId)
            ? $"stack:{rule.StackId}"
            : $"project:{rule.ProjectId}";
    }
}
