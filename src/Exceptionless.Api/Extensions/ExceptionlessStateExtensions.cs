using System;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Extensions.Logging {
    public static class ExceptionlessStateExtensions {
        /// <summary>
        /// Marks the event as being a critical occurrence.
        /// </summary>
        public static ExceptionlessState SetHttpContext(this ExceptionlessState state, HttpContext context) {
            return state.Property("HttpContext", context);
        }
    }
}