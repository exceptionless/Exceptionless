using System;
using System.Web.Http.ExceptionHandling;
using Exceptionless.Enrichments;

namespace Exceptionless.Api.Utility {
    public class ExceptionlessExceptionLogger : ExceptionLogger {
        public override void Log(ExceptionLoggerContext context) {
            var contextData = new ContextData();
            contextData.MarkAsUnhandledError();
            contextData.SetSubmissionMethod("ExceptionLogger");
            contextData.Add("HttpActionContext", context.ExceptionContext.ActionContext);

            context.Exception.ToExceptionless(contextData).Submit();
        }
    }
}
