using System;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Api.Hubs {
    public class ErrorHandlingPipelineModule : HubPipelineModule {
        private readonly ILogger _logger;

        public ErrorHandlingPipelineModule(ILogger<ErrorHandlingPipelineModule> logger) {
            _logger = logger;
        }

        protected override void OnIncomingError(ExceptionContext exceptionContext, IHubIncomingInvokerContext invokerContext) {
            using (_logger.BeginScope(new ExceptionlessState().MarkUnhandled("ErrorHandlingPipelineModule").Tag("SignalR")))
                _logger.LogError(exceptionContext.Error, "Unhandled: {Message}", exceptionContext.Error.Message);
            
            base.OnIncomingError(exceptionContext, invokerContext);
        }
    }
}