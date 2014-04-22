using System;
using Exceptionless.Dependency;
using Exceptionless.Logging;
using Exceptionless.Models;

namespace Exceptionless.Enrichments.Default {
    public class EnvironmentInfo : IEventEnrichment {
        /// <summary>
        /// Enrich the event with additional information.
        /// </summary>
        /// <param name="context">Context information.</param>
        /// <param name="ev">Event to enrich.</param>
        public void Enrich(EventEnrichmentContext context, Event ev) {
            if (ev.Type != Event.KnownTypes.SessionStart)
                return;

            try {
                var collector = context.Resolver.GetEnvironmentInfoCollector();
                ev.Data.Add(Event.KnownDataKeys.EnvironmentInfo, collector.GetEnvironmentInfo());
            } catch (Exception ex) {
                context.Resolver.GetLog().FormattedError(typeof(EnvironmentInfo), ex, "Error adding machine information: {0}", ex.Message);
            }
        }
    }
}