using System;
using System.Linq;
using Exceptionless.Dependency;
using Exceptionless.Logging;
using Exceptionless.Models;

namespace Exceptionless.Enrichments {
    public static class EventEnrichmentManager {
        /// <summary>
        /// Called when the event object is created and can be used to add information to the event.
        /// </summary>
        /// <param name="context">Context information.</param>
        /// <param name="ev">Event that was created.</param>
        public static void Enrich(EventEnrichmentContext context, Event ev) {
            foreach (IEventEnrichment plugin in context.Client.Configuration.Enrichments.ToList()) {
                try {
                    plugin.Enrich(context, ev);
                } catch (Exception ex) {
                    context.Resolver.GetLog().FormattedError(typeof(EventEnrichmentManager), ex, "An error occurred while running {0}.Enrich(): {1}", plugin.GetType().FullName, ex.Message);
                }
            }
        }
    }
}
