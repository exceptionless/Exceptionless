using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventProcessor;

namespace Exceptionless.Core.Pipeline {
    [Priority(6)]
    public class TruncateFieldsAction : EventPipelineActionBase {
        protected override bool IsCritical => true;

        public override Task ProcessAsync(EventContext ctx) {
            ctx.Event.Tags?.RemoveWhere(t => String.IsNullOrEmpty(t) || t.Length > 255);

            if (ctx.Event.Message != null && ctx.Event.Message.Length > 2000)
                ctx.Event.Message = ctx.Event.Message.Truncate(2000);
            else if (String.IsNullOrEmpty(ctx.Event.Message))
                ctx.Event.Message = null;

            if (ctx.Event.Source != null && ctx.Event.Source.Length > 2000)
                ctx.Event.Source = ctx.Event.Source.Truncate(2000);
            else if (String.IsNullOrEmpty(ctx.Event.Source))
                ctx.Event.Source = null;

            return Task.CompletedTask;
        }
    }
}