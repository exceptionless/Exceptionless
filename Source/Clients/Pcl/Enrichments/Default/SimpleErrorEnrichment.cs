using System;
using Exceptionless.Extensions;
using Exceptionless.Models;

namespace Exceptionless.Enrichments.Default {
    public class SimpleErrorEnrichment : IEventEnrichment {
        public void Enrich(EventEnrichmentContext context, Event ev) {
            if (!context.ContextData.ContainsKey(EventEnrichmentContext.KnownContextDataKeys.Exception))
                return;

            var exception = context.ContextData[EventEnrichmentContext.KnownContextDataKeys.Exception] as Exception;
            if (exception == null)
                return;

            ev.Type = Event.KnownTypes.Error;
            ev.Data[Event.KnownDataKeys.SimpleError] = exception.ToSimpleErrorModel();
        }
    }
}