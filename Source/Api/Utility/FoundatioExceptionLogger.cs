using System;
using System.Web.Http.ExceptionHandling;
using Foundatio.Logging;

namespace Exceptionless.Api.Utility {
    public class FoundatioExceptionLogger : ExceptionLogger {
        public override void Log(ExceptionLoggerContext context) {
            Logger.Error()
                .Exception(context.Exception)
                .SetActionContext(context.ExceptionContext.ActionContext)
                .MarkUnhandled("ExceptionLogger")
                .Message("Unhandled: {0}", context.Exception.Message)
                .Write();
        }
    }
}
