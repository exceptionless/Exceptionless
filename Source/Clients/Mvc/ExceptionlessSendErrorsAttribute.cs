using System;
using System.Web.Mvc;
using Exceptionless.Enrichments;

namespace Exceptionless.Mvc {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ExceptionlessSendErrorsAttribute : FilterAttribute, IExceptionFilter {
        public void OnException(ExceptionContext filterContext) {
            var contextData = new ContextData();
            contextData.MarkAsUnhandledError();
            contextData.SetSubmissionMethod("SendErrorsAttribute");
            contextData.Add("HttpContext", filterContext.HttpContext);

            filterContext.Exception.ToExceptionless(contextData).Submit();
        }
    }
}
