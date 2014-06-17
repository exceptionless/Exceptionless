#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Enrichments;
using Exceptionless.ExtendedData;
using Exceptionless.Logging;
using Exceptionless.Models;

namespace Exceptionless.Nancy {
    internal class ExceptionlessNancyEnrichment : IEventEnrichment {
        public void Enrich(EventEnrichmentContext context, Event ev) {
            //error.ExceptionlessClientInfo.Platform = "Nancy";

            var nancyContext = context.Data.GetNancyContext();
            if (nancyContext == null)
                return;

            try {
                NancyRequestInfoCollector.Collect(nancyContext, context.Client.Configuration.DataExclusions);
                ev.AddRequestInfo(nancyContext);

                if (nancyContext.CurrentUser != null && context.Client.Configuration.IncludePrivateInformation)
                    ev.AddUserInfo(nancyContext.CurrentUser.UserName);
            } catch (Exception ex) {
                context.Log.Error(typeof(ExceptionlessNancyEnrichment), ex, "Error adding request info.");
            }
        }
    }
}