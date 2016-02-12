using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(30)]
    public class SimpleErrorPlugin : EventProcessorPluginBase {
        public override Task EventProcessingAsync(EventContext context) {
            if (!context.Event.IsError())
                return Task.CompletedTask;

            SimpleError error = context.Event.GetSimpleError();
            if (error == null)
                return Task.CompletedTask;
            
            if (String.IsNullOrWhiteSpace(context.Event.Message))
                context.Event.Message = error.Message;
            
            if (context.StackSignatureData.Count > 0)
                return Task.CompletedTask;

            // TODO: Parse the stack trace and upgrade this to a full error.
            if (!String.IsNullOrEmpty(error.Type))
                context.StackSignatureData.Add("ExceptionType", error.Type);

            if (!String.IsNullOrEmpty(error.StackTrace))
                context.StackSignatureData.Add("StackTrace", error.StackTrace.ToSHA1());

            return Task.CompletedTask;
        }
    }
}