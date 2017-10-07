using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventProcessor;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline {
    [Priority(6)]
    public class TruncateFieldsAction : EventPipelineActionBase {
        public TruncateFieldsAction(ILoggerFactory loggerFactory = null) : base(loggerFactory) {}

        protected override bool IsCritical => true;

        public override Task ProcessAsync(EventContext ctx) {
            ctx.Event.Tags?.RemoveExcessTags();

            if (ctx.Event.Message != null && ctx.Event.Message.Length > 2000)
                ctx.Event.Message = ctx.Event.Message.Truncate(2000);
            else if (String.IsNullOrEmpty(ctx.Event.Message))
                ctx.Event.Message = null;

            if (ctx.Event.Source != null && ctx.Event.Source.Length > 2000)
                ctx.Event.Source = ctx.Event.Source.Truncate(2000);
            else if (String.IsNullOrEmpty(ctx.Event.Source))
                ctx.Event.Source = null;

            if (!ctx.Event.HasValidReferenceId()) {
                ctx.Event.Data["InvalidReferenceId"] = ctx.Event.ReferenceId;
                ctx.Event.ReferenceId = "invalid-reference-id";
            }

            return Task.CompletedTask;
        }
    }
}