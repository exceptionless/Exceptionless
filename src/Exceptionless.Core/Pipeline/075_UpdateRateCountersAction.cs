using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline;

[Priority(75)]
public class UpdateRateCountersAction : EventPipelineActionBase
{
    private readonly RateNotificationRuleCache _ruleCache;
    private readonly RateCounterService _counterService;

    public UpdateRateCountersAction(
        RateNotificationRuleCache ruleCache,
        RateCounterService counterService,
        AppOptions options,
        ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _ruleCache = ruleCache;
        _counterService = counterService;
        ContinueOnError = true;
    }

    public override async Task ProcessAsync(EventContext ctx)
    {
        // Premium gate — rate notifications require premium features
        if (!ctx.Organization.HasRateNotifications())
            return;

        // Stack must allow notifications
        if (ctx.Stack is null || !ctx.Stack.AllowNotifications)
            return;

        // Load enabled rules for this project
        var rules = await _ruleCache.GetEnabledRulesAsync(ctx.Event.ProjectId);
        if (rules.Count == 0)
            return;

        // RequestInfoPlugin already performs the user-agent work earlier in the pipeline.
        if (ctx.Event.Data?.GetValueOrDefault(Event.KnownDataKeys.RequestInfo) is RequestInfo request &&
            request.Data?.GetValueOrDefault(RequestInfo.KnownDataKeys.IsBot) is true)
            return;

        var counterKeys = GetCounterKeys(ctx.Event, ctx.IsNew, ctx.IsRegression, rules);
        AppDiagnostics.RateCounterKeysIncremented.Add(counterKeys.Count);
        await Task.WhenAll(counterKeys.Select(key => _counterService.IncrementAsync(key)));
    }

    internal static IReadOnlyCollection<string> GetCounterKeys(PersistentEvent ev, bool isNew, bool isRegression, IEnumerable<RateNotificationRule> rules)
    {
        bool isError = ev.IsError();
        bool isCritical = ev.IsCritical();
        var matchedSignals = new HashSet<RateNotificationSignal>();

        matchedSignals.Add(RateNotificationSignal.AllEvents);

        if (isError)
            matchedSignals.Add(RateNotificationSignal.Errors);

        if (isError && isCritical)
            matchedSignals.Add(RateNotificationSignal.CriticalErrors);

        if (isNew && isError)
            matchedSignals.Add(RateNotificationSignal.NewErrors);

        if (isRegression)
            matchedSignals.Add(RateNotificationSignal.Regressions);

        return rules
            .Where(r => matchedSignals.Contains(r.Signal))
            .Where(r => r.Subject == RateNotificationSubject.Project ||
                !String.IsNullOrEmpty(r.StackId) && String.Equals(ev.StackId, r.StackId, StringComparison.Ordinal))
            .Select(BuildCounterKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    internal static string BuildCounterKey(RateNotificationRule rule)
    {
        return rule.Subject switch
        {
            RateNotificationSubject.Project => $"project:{rule.ProjectId}:signal:{rule.Signal}",
            RateNotificationSubject.Stack => $"project:{rule.ProjectId}:stack:{rule.StackId}:signal:{rule.Signal}",
            _ => $"project:{rule.ProjectId}:signal:{rule.Signal}"
        };
    }
}
