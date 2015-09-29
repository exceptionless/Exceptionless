using System;
using Exceptionless.Core.Plugins.EventProcessor;
using NLog.Fluent;

namespace Exceptionless.Core.Pipeline {
    public abstract class EventPipelineActionBase : PipelineActionBase<EventContext> {
        protected virtual bool IsCritical => false;
        protected virtual string[] ErrorTags => new string[0];
        protected virtual string ErrorMessage => null;

        public override bool HandleError(Exception ex, EventContext ctx) {
            string message = ErrorMessage ?? $"Error processing action: {GetType().Name}";
            Log.Error().Project(ctx.Event.ProjectId).Message(message).Exception(ex).Property("data", ctx.Event).Tag(ErrorTags).Critical(IsCritical).Write();

            return ContinueOnError;
        }
    }
}