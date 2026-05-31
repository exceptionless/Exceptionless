using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Exceptionless.Core.Pipeline;

[Priority(75)]
public class UpdateRateCountersAction : EventPipelineActionBase
{
    private readonly RateNotificationRuleCache _ruleCache;
    private readonly RateCounterService _counterService;
    private readonly UserAgentParser _parser;
    private readonly JsonSerializerOptions _jsonOptions;

    public UpdateRateCountersAction(
        RateNotificationRuleCache ruleCache,
        RateCounterService counterService,
        UserAgentParser parser,
        JsonSerializerOptions jsonOptions,
        AppOptions options,
        ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _ruleCache = ruleCache;
        _counterService = counterService;
        _parser = parser;
        _jsonOptions = jsonOptions;
        ContinueOnError = true;
    }

    public override async Task ProcessAsync(EventContext ctx)
    {
        // Premium gate — rate notifications require premium features
        if (!ctx.Organization.HasPremiumFeatures)
            return;

        // Stack must allow notifications
        if (ctx.Stack is null || !ctx.Stack.AllowNotifications)
            return;

        // Load enabled rules for this project
        var rules = await _ruleCache.GetEnabledRulesAsync(ctx.Event.ProjectId);
        if (rules.Count == 0)
            return;

        // Bot check — same pattern as EventNotificationsJob
        var request = ctx.Event.GetRequestInfo(_jsonOptions);
        if (!String.IsNullOrEmpty(request?.UserAgent))
        {
            var botPatterns = ctx.Project.Configuration.Settings
                .GetStringCollection(SettingsDictionary.KnownKeys.UserAgentBotPatterns).ToList();
            var info = await _parser.ParseAsync(request.UserAgent);
            if (info is not null && info.Device.IsSpider || request.UserAgent.AnyWildcardMatches(botPatterns))
            {
                _logger.LogTrace("Skipping rate counter update for bot user agent: {UserAgent}", request.UserAgent);
                return;
            }
        }

        // Build the set of signals matched by this event
        bool isError = ctx.Event.IsError();
        bool isCritical = ctx.Event.IsCritical();
        var matchedSignals = new HashSet<RateNotificationSignal>();

        // AllEvents always matches
        matchedSignals.Add(RateNotificationSignal.AllEvents);

        if (isError)
            matchedSignals.Add(RateNotificationSignal.Errors);

        if (isError && isCritical)
            matchedSignals.Add(RateNotificationSignal.CriticalErrors);

        if (ctx.IsNew && isError)
            matchedSignals.Add(RateNotificationSignal.NewErrors);

        if (ctx.IsRegression)
            matchedSignals.Add(RateNotificationSignal.Regressions);

        // Increment counters for each matching rule
        foreach (var rule in rules)
        {
            if (!matchedSignals.Contains(rule.Signal))
                continue;

            // For Stack subject, only match if this event belongs to the rule's stack
            if (rule.Subject == RateNotificationSubject.Stack)
            {
                if (String.IsNullOrEmpty(rule.StackId) || !String.Equals(ctx.Event.StackId, rule.StackId, StringComparison.Ordinal))
                    continue;
            }

            string counterKey = BuildCounterKey(rule);
            await _counterService.IncrementAsync(counterKey);
        }
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
