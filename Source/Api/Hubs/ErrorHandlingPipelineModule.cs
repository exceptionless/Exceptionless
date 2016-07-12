using System;
using Foundatio.Logging;
using Microsoft.AspNet.SignalR.Hubs;

namespace Exceptionless.Api.Hubs {
    public class ErrorHandlingPipelineModule : HubPipelineModule {
        private readonly ILogger _logger;

        public ErrorHandlingPipelineModule(ILogger<ErrorHandlingPipelineModule> logger) {
            _logger = logger;
        }

        protected override void OnIncomingError(ExceptionContext exceptionContext, IHubIncomingInvokerContext invokerContext) {
            _logger.Error()
                .Exception(exceptionContext.Error)
                .MarkUnhandled("ErrorHandlingPipelineModule")
                .Message("Unhandled: {0}", exceptionContext.Error.Message)
                .Tag("SignalR")
                .Write();
            
            base.OnIncomingError(exceptionContext, invokerContext);
        }
    }
}