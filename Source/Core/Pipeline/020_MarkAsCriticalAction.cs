using System;
using Exceptionless.Core.Plugins.EventProcessor;
using NLog.Fluent;

namespace Exceptionless.Core.Pipeline {
    [Priority(20)]
    public class MarkAsCriticalAction : EventPipelineActionBase {
        protected override bool ContinueOnError { get { return true; } }

        public override void Process(EventContext ctx) {
            if (ctx.Stack == null || !ctx.Stack.OccurrencesAreCritical)
                return;

            Log.Trace().Message("Marking error as critical.").Write();
            ctx.Event.MarkAsCritical();
        }
    }
}