#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Web;
using Exceptionless.ExtendedData;
using Exceptionless.Models;

namespace Exceptionless {
    public static class ExceptionlessWebExtensions {
        /// <summary>
        /// Adds the current request info to the error.
        /// </summary>
        /// <param name="error">The error model.</param>
        /// <param name="client">The client.</param>
        /// <param name="context">The http context to gather information from.</param>
        public static Error AddRequestInfo(this Error error, ExceptionlessClient client, HttpContext context) {
            if (context == null)
                return error;

            error.AddRequestInfo(client, new HttpContextWrapper(context));

            return error;
        }

        /// <summary>
        /// Adds the current request info to the error.
        /// </summary>
        /// <param name="error">The error model.</param>
        /// <param name="client">The client.</param>
        /// <param name="context">The http context to gather information from.</param>
        public static Error AddRequestInfo(this Error error, ExceptionlessClient client, HttpContextBase context = null) {
            if (context == null && HttpContext.Current != null)
                context = new HttpContextWrapper(HttpContext.Current);

            if (context == null)
                return error;

            error.RequestInfo = RequestInfoCollector.Collect(client, context);

            return error;
        }

        /// <summary>
        /// Adds the current request info to the error.
        /// </summary>
        /// <param name="builder">The error builder.</param>
        public static ErrorBuilder AddRequestInfo(this ErrorBuilder builder) {
            builder.Target.AddRequestInfo(builder.Client);
            return builder;
        }

        internal static HttpContextBase GetHttpContext(this IDictionary<string, object> data) {
            if (!data.ContainsKey("HttpContext"))
                return null;

            return data["HttpContext"] as HttpContextBase;
        }
    }
}