using Exceptionless.Core.Models;

namespace Exceptionless.Core.Services;

/// <summary>
/// Compiled, project-scoped counter plan used by the event ingestion hot path.
/// Multiple rules that observe the same signal and subject share one counter.
/// </summary>
public sealed class RateNotificationCounterPlan
{
    public string ProjectId { get; set; } = String.Empty;
    public int RuleCount { get; set; }
    public Dictionary<RateNotificationSignal, string> ProjectCounters { get; set; } = [];
    public Dictionary<string, Dictionary<RateNotificationSignal, string>> StackCounters { get; set; } = new(StringComparer.Ordinal);

    public bool HasCounters => ProjectCounters.Count > 0 || StackCounters.Count > 0;

    public static RateNotificationCounterPlan Compile(string projectId, IEnumerable<RateNotificationRule> rules)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);

        var plan = new RateNotificationCounterPlan { ProjectId = projectId };
        foreach (var rule in rules)
        {
            if (!IsValidRuntimeDefinition(rule, projectId))
                continue;

            plan.RuleCount++;

            if (rule.Subject == RateNotificationSubject.Project)
            {
                plan.ProjectCounters.TryAdd(rule.Signal, BuildCounterKey(rule));
                continue;
            }

            if (rule.Subject != RateNotificationSubject.Stack || String.IsNullOrEmpty(rule.StackId))
                continue;

            if (!plan.StackCounters.TryGetValue(rule.StackId, out var counters))
            {
                counters = [];
                plan.StackCounters.Add(rule.StackId, counters);
            }

            counters.TryAdd(rule.Signal, BuildCounterKey(rule));
        }

        return plan;
    }

    public static bool IsValidRuntimeDefinition(RateNotificationRule rule, string projectId)
    {
        if (String.IsNullOrEmpty(projectId) ||
            String.IsNullOrEmpty(rule.Id) ||
            String.IsNullOrEmpty(rule.OrganizationId) ||
            String.IsNullOrEmpty(rule.ProjectId) ||
            String.IsNullOrEmpty(rule.UserId) ||
            String.IsNullOrWhiteSpace(rule.Name) ||
            rule.Version <= 0 ||
            !rule.IsEnabled || rule.IsDeleted ||
            !String.Equals(rule.ProjectId, projectId, StringComparison.Ordinal) ||
            rule.Threshold <= 0 ||
            rule.Window <= TimeSpan.Zero || rule.Window > RateNotificationRule.MaximumWindow ||
            rule.Cooldown < rule.Window || rule.Cooldown > RateNotificationRule.MaximumCooldown ||
            !Enum.IsDefined(rule.Signal) ||
            !Enum.IsDefined(rule.Subject))
        {
            return false;
        }

        return rule.Subject switch
        {
            RateNotificationSubject.Project => String.IsNullOrEmpty(rule.StackId),
            RateNotificationSubject.Stack => !String.IsNullOrEmpty(rule.StackId),
            _ => false
        };
    }

    public IReadOnlyCollection<string> GetCounterKeys(string? stackId, IEnumerable<RateNotificationSignal> signals)
    {
        StackCounters.TryGetValue(stackId ?? String.Empty, out var stackCounters);
        var keys = new List<string>();

        foreach (var signal in signals)
        {
            if (ProjectCounters.TryGetValue(signal, out string? projectCounter))
                keys.Add(projectCounter);

            if (stackCounters?.TryGetValue(signal, out string? stackCounter) == true)
                keys.Add(stackCounter);
        }

        return keys;
    }

    public static string BuildCounterKey(RateNotificationRule rule)
    {
        return rule.Subject switch
        {
            RateNotificationSubject.Project => $"project:{rule.ProjectId}:signal:{rule.Signal}",
            RateNotificationSubject.Stack => $"project:{rule.ProjectId}:stack:{rule.StackId}:signal:{rule.Signal}",
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule.Subject, "Unsupported rate notification subject.")
        };
    }
}
