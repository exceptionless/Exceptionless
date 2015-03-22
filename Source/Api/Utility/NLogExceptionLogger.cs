using System;
using System.Web.Http.ExceptionHandling;
using NLog.Fluent;
using Logger = NLog.Fluent.Log;

namespace Exceptionless.Api.Utility {
    public class NLogExceptionLogger : ExceptionLogger {
        public override void Log(ExceptionLoggerContext context) {
            string loggerName = context.Exception.TargetSite != null
                && context.Exception.TargetSite.DeclaringType != null ?
                context.Exception.TargetSite.DeclaringType.Name : "NLogExceptionLogger";

            Logger.Error()
                .Exception(context.Exception)
                .ContextProperty("HttpActionContext", context.ExceptionContext.ActionContext)
                .MarkUnhandled("ExceptionLogger")
                .Message("Unhandled: {0}", context.Exception.Message)
                .LoggerName(loggerName)
                .Write();
        }
    }
}
