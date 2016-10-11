using System;
using System.Threading.Tasks;
using Exceptionless.Core.Plugins.EventProcessor;
using Foundatio.Logging;
using Foundatio.Utility;

namespace Exceptionless.Core.Pipeline {
    [Priority(1)]
    public class CheckEventDateAction : EventPipelineActionBase {
        public CheckEventDateAction(ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            ContinueOnError = true;
        }

        public override Task ProcessAsync(EventContext ctx) {
            if (ctx.Organization.RetentionDays <= 0)
                return Task.CompletedTask;

            // If the date is in the future, set it to now using the same offset.
            if (SystemClock.UtcNow < ctx.Event.Date.UtcDateTime)
                ctx.Event.Date = ctx.Event.Date.Subtract(ctx.Event.Date.UtcDateTime - SystemClock.OffsetUtcNow);

            // Discard events that are being submitted outside of the plan retention limit.
            if (SystemClock.UtcNow.Subtract(ctx.Event.Date.UtcDateTime).Days <= ctx.Organization.RetentionDays)
                return Task.CompletedTask;

            _logger.Warn().Project(ctx.Event.ProjectId).Message("Discarding event that occurred outside of your retention limit.").Write();
            ctx.IsCancelled = true;

            return Task.CompletedTask;
        }
    }
}