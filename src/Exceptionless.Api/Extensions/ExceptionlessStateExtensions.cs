using System;
using System.Web.Http.Controllers;

namespace Microsoft.Extensions.Logging {
    public static class ExceptionlessStateExtensions {
        /// <summary>
        /// Marks the event as being a critical occurrence.
        /// </summary>
        public static ExceptionlessState SetActionContext(this ExceptionlessState state, HttpActionContext actionContext) {
            return state.Property("HttpActionContext", actionContext);
        }
    }
}