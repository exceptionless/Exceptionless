using System;
using System.Threading.Tasks;
using Exceptionless.Core.Plugins.EventProcessor;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline {
    [Priority(20)]
    public class MarkAsCriticalAction : EventPipelineActionBase {
        public MarkAsCriticalAction(ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            ContinueOnError = true;
        }

        public override Task ProcessAsync(EventContext ctx) {
            if (ctx.Stack == null || !ctx.Stack.OccurrencesAreCritical)
                return Task.CompletedTask;

            _logger.LogTrace("Marking error as critical.");
            ctx.Event.MarkAsCritical();

            return Task.CompletedTask;
        }
    }
}