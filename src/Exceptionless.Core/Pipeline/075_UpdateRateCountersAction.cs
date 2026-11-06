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
        if (!ShouldIncrement(ctx))
            return;

        // Load the compiled counter plan for this project. Cache misses perform the only rule scan.
        var counterPlan = await _ruleCache.GetCounterPlanAsync(ctx.Event.ProjectId);
        if (!counterPlan.HasCounters)
            return;

        var counterKeys = GetCounterKeys(ctx.Event, ctx.IsNew, ctx.IsRegression, counterPlan);
        AppDiagnostics.RateCounterKeysIncremented.Add(counterKeys.Count);
        await _counterService.IncrementAsync(counterKeys);
    }

    internal static bool ShouldIncrement(EventContext ctx)
    {
        if (ctx.IsCancelled || ctx.IsDiscarded || !ctx.Organization.HasRateNotifications())
            return false;

        if (ctx.Stack is null || !ctx.Stack.AllowNotifications)
            return false;

        // RequestInfoPlugin already performs the user-agent work earlier in the pipeline.
        return ctx.Event.Data?.GetValueOrDefault(Event.KnownDataKeys.RequestInfo) is not RequestInfo request ||
            request.Data?.GetValueOrDefault(RequestInfo.KnownDataKeys.IsBot) is not true;
    }

    internal static IReadOnlyCollection<string> GetCounterKeys(PersistentEvent ev, bool isNew, bool isRegression, RateNotificationCounterPlan counterPlan)
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

        return counterPlan.GetCounterKeys(ev.StackId, matchedSignals);
    }
}
