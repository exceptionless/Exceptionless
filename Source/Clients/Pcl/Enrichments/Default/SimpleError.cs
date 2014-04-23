using System;
using Exceptionless.Models;

namespace Exceptionless.Enrichments.Default {
    public class SimpleError : IEventEnrichment {
        /// <summary>
        /// Enrich the event with additional information.
        /// </summary>
        /// <param name="context">Context information.</param>
        /// <param name="ev">Event to enrich.</param>
        public void Enrich(EventEnrichmentContext context, Event ev) {
            if (!context.ContextData.ContainsKey(EventEnrichmentContext.KnownContextDataKeys.Exception))
                return;

            var exception = ev.Data[EventEnrichmentContext.KnownContextDataKeys.Exception] as Exception;
            if (exception == null)
                return;

            // TODO: create a simple error model.
            ev.SetError(exception);
        }
    }
}