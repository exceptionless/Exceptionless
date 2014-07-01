#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Web;
using Exceptionless.Enrichments;
using Exceptionless.Extensions;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Models.Data;

namespace Exceptionless.Web {
    internal class ExceptionlessWebEnrichment : IEventEnrichment {
        private const string TAGS_HTTP_CONTEXT_NAME = "Exceptionless.Tags";

        public void Enrich(EventEnrichmentContext context, Event ev) {
            HttpContextBase httpContext = context.Data.GetHttpContext();

            // if the context is not passed in, try and grab it
            if (httpContext == null && HttpContext.Current != null)
                httpContext = HttpContext.Current.ToWrapped();

            if (httpContext == null)
                return;

            // ev.ExceptionlessClientInfo.Platform = ".NET Web";
            if (context.Client.Configuration.IncludePrivateInformation
                && httpContext.User != null
                && httpContext.User.Identity.IsAuthenticated)
                ev.SetUserIdentity(httpContext.User.Identity.Name);

            var tags = httpContext.Items[TAGS_HTTP_CONTEXT_NAME] as TagSet;
            if (tags != null)
                ev.Tags.UnionWith(tags);

            RequestInfo requestInfo = null;
            try {
                requestInfo = httpContext.GetRequestInfo(context.Client.Configuration);
            } catch (Exception ex) {
                context.Log.Error(typeof(ExceptionlessWebEnrichment), ex, "Error adding request info.");
            }

            if (requestInfo == null)
                return;

            var httpException = context.Data.GetException() as HttpException;
            if (httpException != null) {
                int httpCode = httpException.GetHttpCode();
                if (httpCode == 404) {
                    ev.Type = Event.KnownTypes.NotFound;
                    ev.Source = requestInfo.GetFullPath(includeHttpMethod: true, includeHost: false, includeQueryString: false);
                    ev.Data.Clear();
                }
            }

            ev.AddRequestInfo(requestInfo);
        }
    }
}