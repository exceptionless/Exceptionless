using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventProcessor;

namespace Exceptionless.Core.Pipeline {
    [Priority(6)]
    public class TruncateFieldsAction : EventPipelineActionBase {
        protected override bool IsCritical { get { return true; } }

        public override void Process(EventContext ctx) {
            if (ctx.Event.Tags != null)
                ctx.Event.Tags.RemoveWhere(t => String.IsNullOrEmpty(t) || t.Length > 255);

            if (ctx.Event.Message != null && ctx.Event.Message.Length > 2000)
                ctx.Event.Message = ctx.Event.Message.Truncate(2000);

            if (ctx.Event.Source != null && ctx.Event.Source.Length > 2000)
                ctx.Event.Source = ctx.Event.Source.Truncate(2000);
        }
    }
}