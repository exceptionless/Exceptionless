using System;
using System.Threading.Tasks;
using Exceptionless.Core.Component;
using Exceptionless.Core.Plugins.EventProcessor;
using Foundatio.Logging;

namespace Exceptionless.Core.Pipeline {
    [Priority(20)]
    public class MarkAsCriticalAction : EventPipelineActionBase {
        public MarkAsCriticalAction() {
            ContinueOnError = true;
        }

        public override Task ProcessAsync(EventContext ctx) {
            if (ctx.Stack == null || !ctx.Stack.OccurrencesAreCritical)
                return TaskHelper.Completed();

            Logger.Trace().Message("Marking error as critical.").Write();
            ctx.Event.MarkAsCritical();

            return TaskHelper.Completed();
        }
    }
}