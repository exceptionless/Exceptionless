using System;
using System.Web.Http.ExceptionHandling;
using Foundatio.Logging;

namespace Exceptionless.Api.Utility {
    public class FoundatioExceptionLogger : ExceptionLogger {
        private readonly ILogger _logger;

        public FoundatioExceptionLogger(ILogger<FoundatioExceptionLogger> logger) {
            _logger = logger;
        }

        public override void Log(ExceptionLoggerContext context) {
            _logger.Error()
                .Exception(context.Exception)
                .SetActionContext(context.ExceptionContext.ActionContext)
                .MarkUnhandled("ExceptionLogger")
                .Message("Unhandled: {0}", context.Exception.Message)
                .Write();
        }
    }
}
