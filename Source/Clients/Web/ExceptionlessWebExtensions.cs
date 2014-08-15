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
using Exceptionless.Models.Data;

namespace Exceptionless {
    public static class ExceptionlessWebExtensions {
        /// <summary>
        /// Get the current request info.
        /// </summary>
        /// <param name="context">The http context to gather information from.</param>
        /// <param name="config">The config.</param>
        public static RequestInfo GetRequestInfo(this HttpContext context, ExceptionlessConfiguration config) {
            if (context == null)
                return null;

            return GetRequestInfo(context.ToWrapped(), config);
        }

        /// <summary>
        /// Adds the current request info.
        /// </summary>
        /// <param name="context">The http context to gather information from.</param>
        /// <param name="config">The config.</param>
        public static RequestInfo GetRequestInfo(this HttpContextBase context, ExceptionlessConfiguration config) {
            if (context == null && HttpContext.Current != null)
                context = HttpContext.Current.ToWrapped();

            return RequestInfoCollector.Collect(context, config);
        }

        /// <summary>
        /// Adds the current request info to the event.
        /// </summary>
        /// <param name="ev">The event model.</param>
        /// <param name="client">The client.</param>
        /// <param name="context">The http context to gather information from.</param>
        public static Event AddRequestInfo(this Event ev, ExceptionlessClient client, HttpContext context) {
            if (context == null)
                return ev;

            ev.AddRequestInfo(client, context.ToWrapped());

            return ev;
        }

        /// <summary>
        /// Adds the current request info to the event.
        /// </summary>
        /// <param name="ev">The event model.</param>
        /// <param name="client">The client.</param>
        /// <param name="context">The http context to gather information from.</param>
        public static Event AddRequestInfo(this Event ev, ExceptionlessClient client, HttpContextBase context = null) {
            if (context == null && HttpContext.Current != null)
                context = HttpContext.Current.ToWrapped();

            if (context == null)
                return ev;

            ev.AddRequestInfo(RequestInfoCollector.Collect(context, client.Configuration));

            return ev;
        }

        /// <summary>
        /// Adds the current request info to the event.
        /// </summary>
        /// <param name="builder">The event builder.</param>
        public static EventBuilder AddRequestInfo(this EventBuilder builder) {
            builder.Target.AddRequestInfo(builder.Client);
            return builder;
        }

        internal static HttpContextBase ToWrapped(this HttpContext context) {
            return new HttpContextWrapper(context);
        }

        internal static HttpContextBase GetHttpContext(this IDictionary<string, object> data) {
            if (!data.ContainsKey("HttpContext"))
                return null;

            return data["HttpContext"] as HttpContextBase;
        }
    }
}