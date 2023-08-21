﻿using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.DateTimeExtensions;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline;

[Priority(1)]
public class CheckEventDateAction : EventPipelineActionBase
{
    public CheckEventDateAction(AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        ContinueOnError = true;
    }

    public override Task ProcessAsync(EventContext ctx)
    {
        // If the date is in the future, set it to now using the same offset.
        if (SystemClock.UtcNow.IsBefore(ctx.Event.Date.UtcDateTime))
            ctx.Event.Date = ctx.Event.Date.Subtract(ctx.Event.Date.UtcDateTime - SystemClock.OffsetUtcNow);

        // Discard events that are being submitted outside of the plan retention limit.
        double eventAgeInDays = SystemClock.UtcNow.Subtract(ctx.Event.Date.UtcDateTime).TotalDays;
        if (eventAgeInDays > 3 || ctx.Organization.RetentionDays > 0 && eventAgeInDays > ctx.Organization.RetentionDays)
        {
            _logger.LogInformation("Discarding event that occurred more than three days ago or outside of organization retention limit.");

            ctx.IsCancelled = true;
            ctx.IsDiscarded = true;
        }

        return Task.CompletedTask;
    }
}
