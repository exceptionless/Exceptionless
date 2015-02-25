using System;
using System.Web.Http.ExceptionHandling;
using Exceptionless.Enrichments;

namespace Exceptionless.Api.Utility {
    public class ExceptionlessExceptionLogger : ExceptionLogger {
        private readonly ExceptionlessClient _exceptionlessClient;

        public ExceptionlessExceptionLogger(ExceptionlessClient exceptionlessClient) {
            _exceptionlessClient = exceptionlessClient;
        }

        public override void Log(ExceptionLoggerContext context) {
            var contextData = new ContextData();
            contextData.MarkAsUnhandledError();
            contextData.SetSubmissionMethod("ExceptionLogger");
            contextData.Add("HttpActionContext", context.ExceptionContext.ActionContext);

           context.Exception.ToExceptionless(contextData, _exceptionlessClient).Submit();
        }
    }
}
