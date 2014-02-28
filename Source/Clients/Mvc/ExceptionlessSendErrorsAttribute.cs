using System;
using System.Web.Mvc;

namespace Exceptionless.Mvc {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ExceptionlessSendErrorsAttribute : FilterAttribute, IExceptionFilter {
        public void OnException(ExceptionContext filterContext) {
            ExceptionlessClient.Current.ProcessUnhandledException(filterContext.Exception, "SendErrorsAttribute", true, filterContext.HttpContext.ToDictionary());
        }
    }
}
