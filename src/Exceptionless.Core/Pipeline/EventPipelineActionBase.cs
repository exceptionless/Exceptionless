using System;
using System.Collections.Generic;
using Exceptionless.Core.Plugins.EventProcessor;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline {
    public abstract class EventPipelineActionBase : PipelineActionBase<EventContext> {
        public EventPipelineActionBase(ILoggerFactory loggerFactory = null) : base(loggerFactory) {}

        protected virtual bool IsCritical => false;
        protected virtual string[] ErrorTags => new string[0];
        protected virtual string ErrorMessage => null;

        public override bool HandleError(Exception ex, EventContext ctx) {
            var ev = new { ctx.Event.Date, ctx.Event.StackId, ctx.Event.Type, ctx.Event.Source, ctx.Event.Message, ctx.Event.Value, ctx.Event.Geo, ctx.Event.ReferenceId, ctx.Event.Tags };
            using (_logger.BeginScope(new Dictionary<string, object> { { "Event", ev }, { "Tags", ErrorTags }})) {
                if (IsCritical)
                    _logger.LogCritical(ex, "Error processing action: {TypeName} Message: {Message}", GetType().Name, ex.Message);
                else
                    _logger.LogError(ex, "Error processing action: {TypeName} Message: {Message}", GetType().Name, ex.Message);
            }

            return ContinueOnError;
        }
    }
}