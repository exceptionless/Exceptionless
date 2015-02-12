#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Enrichments;
using Exceptionless.Extensions;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Models.Data;

namespace Exceptionless.Nancy {
    internal class ExceptionlessNancyEnrichment : IEventEnrichment {
        public void Enrich(EventEnrichmentContext context, Event ev) {
            //error.ExceptionlessClientInfo.Platform = "Nancy";

            var nancyContext = context.Data.GetNancyContext();
            if (nancyContext == null)
                return;

            if (nancyContext.CurrentUser != null && context.Client.Configuration.IncludePrivateInformation)
                ev.SetUserIdentity(nancyContext.CurrentUser.UserName);

            RequestInfo requestInfo = null;
            try {
                requestInfo = nancyContext.GetRequestInfo(context.Client.Configuration);
            } catch (Exception ex) {
                context.Log.Error(typeof(ExceptionlessNancyEnrichment), ex, "Error adding request info.");
            }

            if (requestInfo == null)
                return;

            if (ev.Type == Event.KnownTypes.NotFound) {
                ev.Source = requestInfo.GetFullPath(includeHttpMethod: true, includeHost: false, includeQueryString: false);
                ev.Data.Clear();
            }

            ev.AddRequestInfo(requestInfo);
        }
    }
}