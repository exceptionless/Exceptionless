using System;
using Exceptionless.Models;

namespace Exceptionless.Enrichments.Default {
    public class ReferenceIdEnrichment : IEventEnrichment {
        public void Enrich(EventEnrichmentContext context, Event ev) {
            if (ev.Type != Event.KnownTypes.Error)
                return;

            ev.ReferenceId = Guid.NewGuid().ToString("N").Substring(0, 10);
        }
    }
}