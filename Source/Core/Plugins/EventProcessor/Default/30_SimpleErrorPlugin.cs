using System;
using CodeSmith.Core.Component;
using CodeSmith.Core.Extensions;
using Exceptionless.Models.Data;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(30)]
    public class SimpleErrorPlugin : EventProcessorPluginBase {
        public override void EventProcessing(EventContext context) {
            if (!context.Event.IsError())
                return;

            SimpleError error = context.Event.GetSimpleError();
            if (error == null)
                return;
            
            if (String.IsNullOrWhiteSpace(context.Event.Message))
                context.Event.Message = error.Message;

            // TODO: Parse the stack trace and upgrade this to a full error.
            context.StackSignatureData.Add("ExceptionType", error.Type);
            context.StackSignatureData.Add("StackTrace", error.StackTrace.ToSHA1());
        }
    }
}