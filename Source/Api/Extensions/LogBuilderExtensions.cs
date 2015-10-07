using System;
using System.Web.Http.Controllers;

namespace Foundatio.Logging {
    public static class LogBuilderExtensions {
        /// <summary>
        /// Marks the event as being a critical occurrence.
        /// </summary>
        public static ILogBuilder SetActionContext(this ILogBuilder builder, HttpActionContext actionContext) {
            return builder.ContextProperty("HttpActionContext", actionContext);
        }
    }
}