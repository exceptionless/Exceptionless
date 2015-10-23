using System;
using Foundatio.Logging;
using Microsoft.AspNet.SignalR.Hubs;

namespace Exceptionless.Api.Hubs {
    public class ErrorHandlingPipelineModule : HubPipelineModule {
        protected override void OnIncomingError(ExceptionContext exceptionContext, IHubIncomingInvokerContext invokerContext) {
            Logger.Error()
                .Exception(exceptionContext.Error)
                .MarkUnhandled("ErrorHandlingPipelineModule")
                .Message("Unhandled: {0}", exceptionContext.Error.Message)
                .Tag("SignalR")
                .Write();
            
            base.OnIncomingError(exceptionContext, invokerContext);
        }
    }
}