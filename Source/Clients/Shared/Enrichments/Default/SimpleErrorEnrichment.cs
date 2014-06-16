using System;
using Exceptionless.Extensions;
using Exceptionless.Models;

namespace Exceptionless.Enrichments.Default {
    public class SimpleErrorEnrichment : IEventEnrichment {
        public void Enrich(EventEnrichmentContext context, Event ev) {
            var exception = context.Data.GetException();
            if (exception == null)
                return;

            ev.Type = Event.KnownTypes.Error;
            ev.Data[Event.KnownDataKeys.SimpleError] = exception.ToSimpleErrorModel();
        }
    }
}