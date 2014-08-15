using System;
using Exceptionless.Dependency;
using Exceptionless.Logging;
using Exceptionless.Models;

namespace Exceptionless.Enrichments.Default {
    public class ActionEnrichment : IEventEnrichment {
        private readonly Action<EventEnrichmentContext, Event> _enrichmentAction;

        public ActionEnrichment(Action<EventEnrichmentContext, Event> enrichmentAction) {
            _enrichmentAction = enrichmentAction;
        }

        public void Enrich(EventEnrichmentContext context, Event ev) {
            try {
                _enrichmentAction(context, ev);
            } catch (Exception ex) {
                context.Resolver.GetLog().FormattedError(typeof(ActionEnrichment), ex, "An error occurred while running an custom enrichment: {0}", ex.Message);
            }
        }
    }
}