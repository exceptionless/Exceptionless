#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using Exceptionless.ExtendedData;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Exceptionless.WebApi;

namespace Exceptionless {
    public static class ExceptionlessWebApiExtensions {
        public static void RegisterWebApi(this ExceptionlessClient client, HttpConfiguration config) {
            client.Startup();
            client.Configuration.AddEnrichment<ExceptionlessWebApiEnrichment>();
            client.Configuration.IncludePrivateInformation = true;

#if WEBAPI21
            config.Services.Add(typeof(IExceptionLogger), new ExceptionlessExceptionLogger());
#endif

            ReplaceHttpErrorHandler(config);
        }

        public static void UnregisterWebApi(this ExceptionlessClient client) {
            client.Shutdown();
            client.Configuration.RemoveEnrichment<ExceptionlessWebApiEnrichment>();
        }

        private static void ReplaceHttpErrorHandler(HttpConfiguration config) {
            FilterInfo filter = config.Filters.FirstOrDefault(f => f.Instance is IExceptionFilter);
            var handler = new ExceptionlessHandleErrorAttribute();

            if (filter != null) {
                if (filter.Instance is ExceptionlessHandleErrorAttribute)
                    return;

                config.Filters.Remove(filter.Instance);

                handler.WrappedFilter = (IExceptionFilter)filter.Instance;
            }

            config.Filters.Add(handler);
        }

        /// <summary>
        /// Adds the current request info.
        /// </summary>
        /// <param name="context">The http action context to gather information from.</param>
        /// <param name="config">The config.</param>
        public static RequestInfo GetRequestInfo(this HttpActionContext context, ExceptionlessConfiguration config) {
            return RequestInfoCollector.Collect(context, config.DataExclusions);
        }

        /// <summary>
        /// Adds the current request info as extended data to the event.
        /// </summary>
        /// <param name="ev">The event model.</param>
        /// <param name="context">The http action context to gather information from.</param>
        public static Event AddHttpRequestInfo(this Event ev, HttpActionContext context) {
            if (context == null)
                return ev;

            ev.AddRequestInfo(context.GetRequestInfo(ExceptionlessClient.Default.Configuration));

            return ev;
        }

        /// <summary>
        /// Adds the current request info as extended data to the event.
        /// </summary>
        /// <param name="builder">The event builder.</param>
        /// <param name="context">The http action context to gather information from.</param>
        public static EventBuilder AddHttpRequestInfo(this EventBuilder builder, HttpActionContext context) {
            builder.Target.AddHttpRequestInfo(context);
            return builder;
        }

        internal static HttpActionContext GetHttpActionContext(this IDictionary<string, object> data) {
            if (!data.ContainsKey("HttpActionContext"))
                return null;

            return data["HttpActionContext"] as HttpActionContext;
        }
    }
}