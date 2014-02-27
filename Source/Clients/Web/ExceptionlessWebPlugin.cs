#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Web;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Plugins;

namespace Exceptionless.Web {
    internal class ExceptionlessWebPlugin : ExceptionlessPlugin {
        private const string TAGS_HTTP_CONTEXT_NAME = "Exceptionless.Tags";

        public override void AfterCreated(ExceptionlessPluginContext context, Error error, Exception exception) {
            base.AfterCreated(context, error, exception);

            HttpContextBase httpContext = context.Data.GetHttpContext();

            // if the context is not passed in, try and grab it
            if (httpContext == null && HttpContext.Current != null)
                httpContext = HttpContext.Current.ToWrapped();

            if (httpContext == null)
                return;

            error.ExceptionlessClientInfo.Platform = ".NET Web";

            var httpException = exception as HttpException;
            if (httpException != null) {
                int httpCode = httpException.GetHttpCode();
                error.Code = httpCode.ToString();

                // only record the bare minimum information on 404s.
                if (error.Code == "404") {
                    error.StackTrace.Clear();
                    error.Inner = null;
                    error.TargetMethod = null;
                }
            }

            var tags = httpContext.Items[TAGS_HTTP_CONTEXT_NAME] as TagSet;
            if (tags != null)
                error.Tags.UnionWith(tags);
        }

        public override void AddDefaultInformation(ExceptionlessPluginContext context, Error error) {
            base.AddDefaultInformation(context, error);
            error.UserName = null;

            HttpContextBase httpContext = context.Data.GetHttpContext();

            // if the context is not passed in, try and grab it
            if (httpContext == null && HttpContext.Current != null)
                httpContext = HttpContext.Current.ToWrapped();

            if (httpContext == null)
                return;

            if (context.Client.Configuration.IncludePrivateInformation
                && httpContext.User != null
                && httpContext.User.Identity.IsAuthenticated)
                error.UserName = httpContext.User.Identity.Name;

            try {
                error.AddRequestInfo(context.Client, httpContext);
            } catch (Exception ex) {
                context.Client.Log.Error(typeof(ExceptionlessWebPlugin), ex, "Error adding request info.");
            }
        }
    }
}