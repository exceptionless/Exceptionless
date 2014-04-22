using System;
using Exceptionless.Models;

namespace Exceptionless.Enrichments.Default {
    public class EnvironmentInfoEventEnrichment : IEventEnrichment {
        /// <summary>
        /// Enrich the event with additional information.
        /// </summary>
        /// <param name="context">Context information.</param>
        /// <param name="ev">Event to enrich.</param>
        public void Enrich(EventEnrichmentContext context, Event ev) {
            try {
                ev.EnvironmentInfo = EnvironmentInfoCollector.Collect();
            } catch (Exception ex) {
                context.Client.Log.FormattedError(typeof(EnvironmentInfoEventEnrichment), ex, "Error adding machine information: {0}", ex.Message);
            }
        }
    }
}