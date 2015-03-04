using System;
using Exceptionless.Core.Plugins.EventProcessor;
using NLog.Fluent;

namespace Exceptionless.Core.Pipeline {
    [Priority(1)]
    public class CheckEventDateAction : EventPipelineActionBase {
        protected override bool ContinueOnError { get { return true; } }

        public override void Process(EventContext ctx) {
            if (ctx.Organization.RetentionDays <= 0)
                return;

            // If the date is in the future, set it to now using the same offset.
            if (DateTimeOffset.Now.UtcDateTime < ctx.Event.Date.UtcDateTime)
                ctx.Event.Date = ctx.Event.Date.Subtract(ctx.Event.Date.UtcDateTime - DateTimeOffset.UtcNow);

            // Discard events that are being submitted outside of the plan retention limit.
            if (DateTimeOffset.Now.UtcDateTime.Subtract(ctx.Event.Date.UtcDateTime).Days <= ctx.Organization.RetentionDays)
                return;

            Log.Info().Project(ctx.Event.ProjectId).Message("Discarding event that occurred outside of your retention limit.").Write();
            ctx.IsCancelled = true;
        }
    }
}