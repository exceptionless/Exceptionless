using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace Exceptionless.Mvc {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ExceptionlessSendErrorsAttribute : FilterAttribute, IExceptionFilter {
        public void OnException(ExceptionContext filterContext) {
            var contextData = new Dictionary<string, object> {
                    { "HttpContext", filterContext.HttpContext }
                };
            ExceptionlessClient.Current.ProcessUnhandledException(filterContext.Exception, "SendErrorsAttribute", true, contextData);
        }
    }
}
