#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Enrichments;
using Exceptionless.Models;

namespace Exceptionless.Mvc {
    internal class ExceptionlessMvcEnrichment : IEventEnrichment {
        public void Enrich(EventEnrichmentContext context, Event ev) {
            if (context.Data.GetHttpContext() == null)
                return;

            //error.ExceptionlessClientInfo.Platform = ".NET MVC";
        }
    }
}