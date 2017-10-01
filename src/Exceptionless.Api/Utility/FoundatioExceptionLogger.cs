using System;
using System.Web.Http.ExceptionHandling;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Api.Utility {
    public class FoundatioExceptionLogger : ExceptionLogger {
        private readonly ILogger _logger;

        public FoundatioExceptionLogger(ILogger<FoundatioExceptionLogger> logger) {
            _logger = logger;
        }

        public override void Log(ExceptionLoggerContext context) {
            using (_logger.BeginScope(new ExceptionlessState().MarkUnhandled("ExceptionLogger").SetActionContext(context.ExceptionContext.ActionContext)))
                _logger.LogError(context.Exception, "Unhandled: {Message}", context.Exception.Message);
        }
    }
}
