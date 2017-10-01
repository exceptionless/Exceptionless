using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(80)]
    public sealed class AngularPlugin : EventProcessorPluginBase {
        public AngularPlugin(ILoggerFactory loggerFactory = null) : base(loggerFactory) {}

        public override Task EventProcessingAsync(EventContext context) {
            if (!context.Event.IsError())
                return Task.CompletedTask;

            var error = context.Event.GetError();
            if (error == null)
                return Task.CompletedTask;

            string submissionMethod = context.Event.GetSubmissionMethod();
            if (submissionMethod == null || !String.Equals("$exceptionHandler", submissionMethod))
                return Task.CompletedTask;

            if (context.StackSignatureData.Count != 1 || !context.StackSignatureData.ContainsKey("NoStackingInformation"))
                return Task.CompletedTask;

            string cause = context.Event.Message;
            if (String.IsNullOrEmpty(cause))
                return Task.CompletedTask;

            if (cause.StartsWith("Possibly unhandled rejection")) {
                context.StackSignatureData.Remove("NoStackingInformation");
                context.StackSignatureData.Add("ExceptionType", error.Type ?? "Error");
                context.StackSignatureData.Add("Source", "unhandledRejection");

                error.Data[Error.KnownDataKeys.TargetInfo] = new SettingsDictionary(context.StackSignatureData);
            }

            return Task.CompletedTask;
        }
    }
}