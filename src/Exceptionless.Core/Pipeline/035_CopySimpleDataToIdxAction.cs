using System;
using System.Threading.Tasks;
using Exceptionless.Core.Plugins.EventProcessor;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline {
    [Priority(40)]
    public class CopySimpleDataToIdxAction : EventPipelineActionBase {
        public CopySimpleDataToIdxAction(ILoggerFactory loggerFactory = null) : base(loggerFactory) {}

        public override Task ProcessAsync(EventContext ctx) {
            if (!ctx.Organization.HasPremiumFeatures)
                return Task.CompletedTask;

            // TODO: Do we need a pipeline action to trim keys and remove null values that may be sent by other native clients.
            ctx.Event.CopyDataToIndex();

            return Task.CompletedTask;
        }
    }
}