using System;
using Exceptionless.Extensions;
using Exceptionless.Models;

namespace Exceptionless.Enrichments.Default {
    public class SimpleError : IEventEnrichment {
        public void Enrich(EventEnrichmentContext context, Event ev) {
            if (!context.ContextData.ContainsKey(EventEnrichmentContext.KnownContextDataKeys.Exception))
                return;

            var exception = context.ContextData[EventEnrichmentContext.KnownContextDataKeys.Exception] as Exception;
            if (exception == null)
                return;

            ev.SetError(exception.ToSimpleErrorModel());
        }
    }
}