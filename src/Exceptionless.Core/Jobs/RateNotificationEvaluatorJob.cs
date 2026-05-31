using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
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
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var scanFrom = now.Subtract(ScanWindow);

        // Round scanFrom down to minute boundary; stop 1 minute before now (current bucket may be incomplete)
        var fromMinute = new DateTime(scanFrom.Year, scanFrom.Month, scanFrom.Day, scanFrom.Hour, scanFrom.Minute, 0, DateTimeKind.Utc);
        var toMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc).AddMinutes(-1);

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

        if (allCounterKeys.Count == 0)
        {
            _logger.LogDebug("No active counter keys found in scan window");
            return JobResult.Success;
        }

        // For each unique counter key, evaluate all matching rules
        foreach (string counterKey in allCounterKeys)
        {
            if (context.CancellationToken.IsCancellationRequested)
                return JobResult.Cancelled;

            await EvaluateCounterKeyAsync(counterKey, now, context.CancellationToken);
        }

        _logger.LogInformation("Finished evaluating rate notification rules");
        return JobResult.Success;
    }

    private async Task EvaluateCounterKeyAsync(string counterKey, DateTime now, CancellationToken ct)
    {
        // Parse projectId from counter key to load rules
        string? projectId = ParseProjectIdFromCounterKey(counterKey);
        if (projectId is null)
        {
            _logger.LogWarning("Unable to parse projectId from counter key: {CounterKey}", counterKey);
            return;
        }

        // Load enabled rules for project matching this counter key
        var allProjectRules = await _ruleRepository.GetEnabledByProjectIdAsync(projectId, o => o.PageLimit(1000));

        // Filter rules matching this counter key
        var matchingRules = allProjectRules.Documents
            .Where(r => CounterKeyMatchesRule(counterKey, r))
            .ToList();

        if (matchingRules.Count == 0)
            return;

        // Load org once per project for premium check
        string? organizationId = matchingRules.First().OrganizationId;
        var org = await _organizationRepository.GetByIdAsync(organizationId, o => o.Cache());
        if (org is null)
            return;

        if (!org.HasPremiumFeatures)
            return;

        foreach (var rule in matchingRules)
        {
            if (ct.IsCancellationRequested)
                return;

            await EvaluateRuleAsync(rule, counterKey, now, ct);
        }
    }

    private async Task EvaluateRuleAsync(RateNotificationRule rule, string counterKey, DateTime now, CancellationToken ct)
    {
        if (!rule.IsEnabled)
            return;

        // Skip if actively snoozed
        if (rule.SnoozedUntilUtc.HasValue && rule.SnoozedUntilUtc.Value > now)
        {
            _logger.LogDebug("Skipping snoozed rule {RuleId} snoozed until {SnoozedUntil}", rule.Id, rule.SnoozedUntilUtc);
            return;
        }

        var windowStartUtc = now.Subtract(rule.Window);

        // SNOOZE BACK-ALERT FIX:
        // If the rule was recently un-snoozed (SnoozedUntilUtc is set and in the past), use that as the
        // effective window start to ignore traffic that occurred during the snooze period.
        var effectiveWindowStartUtc = rule.SnoozedUntilUtc.HasValue && rule.SnoozedUntilUtc.Value > windowStartUtc
            ? rule.SnoozedUntilUtc.Value
            : windowStartUtc;

        var observedCount = await _counterService.SumBucketsAsync(counterKey, effectiveWindowStartUtc, now, ct);

        if (observedCount < rule.Threshold)
        {
            _logger.LogDebug("Rule {RuleId}: observed={Observed} < threshold={Threshold}, skipping", rule.Id, observedCount, rule.Threshold);
            return;
        }

        // Build subject key (for cooldown scoping)
        string subjectKey = BuildSubjectKey(rule);

        // Check cooldown
        if (await _counterService.IsOnCooldownAsync(rule.Id, subjectKey, ct))
        {
            _logger.LogDebug("Rule {RuleId} is on cooldown for subject {SubjectKey}", rule.Id, subjectKey);
            return;
        }

        // Enqueue notification
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
            WindowEndUtc = now,
            ObservedCount = observedCount,
            Threshold = rule.Threshold
        });

        // Set cooldown
        await _counterService.SetCooldownAsync(rule.Id, subjectKey, rule.Cooldown, ct);

        // Update LastFiredUtc
        rule.LastFiredUtc = now;
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

    /// <summary>Returns true if the counter key was generated by the given rule.</summary>
    private static bool CounterKeyMatchesRule(string counterKey, RateNotificationRule rule)
    {
        string expected = UpdateRateCountersAction.BuildCounterKey(rule);
        return String.Equals(counterKey, expected, StringComparison.Ordinal);
    }

    private static string BuildSubjectKey(RateNotificationRule rule)
    {
        return rule.Subject == RateNotificationSubject.Stack && !String.IsNullOrEmpty(rule.StackId)
            ? $"stack:{rule.StackId}"
            : $"project:{rule.ProjectId}";
    }
}
