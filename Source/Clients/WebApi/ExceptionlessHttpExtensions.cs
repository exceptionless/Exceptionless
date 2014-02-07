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
using Exceptionless.WebApi;

namespace Exceptionless {
    public static class ExceptionlessHttpExtensions {
        public static void RegisterWebApi(this ExceptionlessClient client, HttpConfiguration config) {
            client.RegisterPlugin(new ExceptionlessWebApiPlugin());
            client.Startup();
            client.Configuration.IncludePrivateInformation = true;

            ReplaceHttpErrorHandler(config);
        }

        public static void UnregisterWebApi(this ExceptionlessClient client) {
            client.UnregisterPlugin(typeof(ExceptionlessWebApiPlugin).FullName);
            client.Shutdown();
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
        /// Adds the current request info as extended data to the error.
        /// </summary>
        /// <param name="error">The error model.</param>
        /// <param name="context">The http action context to gather information from.</param>
        public static Error AddHttpRequestInfo(this Error error, HttpActionContext context) {
            if (context == null)
                return error;

            // TODO: Create HttpActionContext version of request info.
            //context.Request.Headers.GetCookies();
            error.RequestInfo = RequestInfoCollector.Collect(context, ExceptionlessClient.Current);

            return error;
        }

        /// <summary>
        /// Adds the current request info as extended data to the error.
        /// </summary>
        /// <param name="builder">The error builder.</param>
        /// <param name="context">The http action context to gather information from.</param>
        public static ErrorBuilder AddHttpRequestInfo(this ErrorBuilder builder, HttpActionContext context) {
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