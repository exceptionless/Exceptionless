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
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Utility;
using Foundatio.Resilience;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs;

[Job(Description = "Evaluates rate notification rules and enqueues notifications.", IsContinuous = false)]
public class RateNotificationEvaluatorJob : JobWithLockBase
{
    private readonly RateCounterService _counterService;
    private readonly IRateNotificationRuleRepository _ruleRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IQueue<RateNotification> _notificationQueue;
    private readonly ILockProvider _lockProvider;

    /// <summary>How far back to scan for active counter minutes.</summary>
    private static readonly TimeSpan ScanWindow = TimeSpan.FromHours(2);

    public RateNotificationEvaluatorJob(
        RateCounterService counterService,
        IRateNotificationRuleRepository ruleRepository,
        IOrganizationRepository organizationRepository,
        IProjectRepository projectRepository,
        IQueue<RateNotification> notificationQueue,
        ILockProvider lockProvider,
        TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider,
        ILoggerFactory loggerFactory) : base(timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        _counterService = counterService;
        _ruleRepository = ruleRepository;
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
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
        int scannedMinutes = 0;
        for (var minute = fromMinute; minute <= toMinute; minute = minute.AddMinutes(1))
        {
            var keys = await _counterService.GetActiveCounterKeysAsync(minute, context.CancellationToken);
            foreach (var key in keys)
                allCounterKeys.Add(key);

            if (++scannedMinutes % 30 == 0)
                await context.RenewLockAsync();
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

            await context.RenewLockAsync();
            await EvaluateProjectAsync(projectCounterKeys.Key, projectCounterKeys, currentMinute, context);
        }

        await _counterService.SetLastEvaluatedMinuteAsync(toMinute, context.CancellationToken);
        _logger.LogInformation("Finished evaluating rate notification rules");
        return JobResult.Success;
    }

    private async Task EvaluateProjectAsync(string projectId, IEnumerable<string> counterKeys, DateTime evaluationEndUtc, JobContext context)
    {
        var ct = context.CancellationToken;
        var project = await _projectRepository.GetByIdAsync(projectId, o => o.Cache());
        if (project is null)
            return;

        var organization = await _organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
        if (organization is null || !organization.HasRateNotifications())
            return;

        var allProjectRules = await GetAllEnabledRulesAsync(projectId, context);
        var validProjectRules = allProjectRules
            .Where(rule => String.Equals(rule.OrganizationId, project.OrganizationId, StringComparison.Ordinal) &&
                RateNotificationCounterPlan.IsValidRuntimeDefinition(rule, projectId))
            .ToList();
        if (validProjectRules.Count != allProjectRules.Count)
            _logger.LogWarning("Skipping {InvalidRuleCount} invalid rate notification rules for project {ProjectId}", allProjectRules.Count - validProjectRules.Count, projectId);

        if (validProjectRules.Count == 0)
            return;

        var rulesByCounterKey = validProjectRules
            .GroupBy(RateNotificationCounterPlan.BuildCounterKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        int evaluatedRules = 0;
        foreach (string counterKey in counterKeys)
        {
            if (!rulesByCounterKey.TryGetValue(counterKey, out var matchingRules))
                continue;

            foreach (var rule in matchingRules)
            {
                if (ct.IsCancellationRequested)
                    return;

                await EvaluateRuleAsync(rule, counterKey, evaluationEndUtc, ct);
                if (++evaluatedRules % 100 == 0)
                    await context.RenewLockAsync();
            }
        }
    }

    private async Task<List<RateNotificationRule>> GetAllEnabledRulesAsync(string projectId, JobContext context)
    {
        var ct = context.CancellationToken;
        var rules = new List<RateNotificationRule>();
        var results = await _ruleRepository.GetEnabledByProjectIdAsync(projectId, o => o.SearchAfterPaging().PageLimit(500));
        do
        {
            ct.ThrowIfCancellationRequested();
            rules.AddRange(results.Documents);
            await context.RenewLockAsync();
        } while (await results.NextPageAsync());

        return rules;
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

        if (await _counterService.IsOnCooldownAsync(rule.Id, subjectKey, ct))
        {
            _logger.LogDebug("Rule {RuleId} is on cooldown for subject {SubjectKey}", rule.Id, subjectKey);
            return;
        }

        // Use a short-lived claim while enqueueing. The full cooldown starts only after the
        // queue accepts the notification, so a process crash cannot silence the rule for hours.
        if (!await _counterService.TryAcquireEvaluationClaimAsync(rule.Id, subjectKey, ct))
            return;

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
            await _counterService.SetCooldownAsync(rule.Id, subjectKey, rule.Cooldown, ct);
            AppDiagnostics.RateNotificationsEnqueued.Add(1);
        }
        finally
        {
            await _counterService.RemoveEvaluationClaimAsync(rule.Id, subjectKey, ct);
        }

        // LastFiredUtc is evaluator-owned bookkeeping. Patch only that field so a concurrent
        // user edit, disable, or snooze cannot be overwritten by this previously loaded snapshot.
        await _ruleRepository.PatchAsync(
            rule.Id,
            new PartialPatch(new { last_fired_utc = evaluationEndUtc }),
            o => o.Notifications(false));

        _logger.LogInformation("Rate notification fired: rule={RuleId} project={ProjectId} observed={Observed} threshold={Threshold}",
            rule.Id, rule.ProjectId, observedCount, rule.Threshold);
    }

    /// <summary>Parses the projectId from a counter key of the form: project:{projectId}:... </summary>
    private static string? ParseProjectIdFromCounterKey(string counterKey)
    {
        const string prefix = "project:";
        if (!counterKey.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        int start = prefix.Length;
        int end = counterKey.IndexOf(':', start);
        if (end < 0)
            return null;

        string projectId = counterKey[start..end];
        return ObjectId.IsValid(projectId) ? projectId : null;
    }

    private static string BuildSubjectKey(RateNotificationRule rule)
    {
        return rule.Subject == RateNotificationSubject.Stack && !String.IsNullOrEmpty(rule.StackId)
            ? $"stack:{rule.StackId}"
            : $"project:{rule.ProjectId}";
    }
}
