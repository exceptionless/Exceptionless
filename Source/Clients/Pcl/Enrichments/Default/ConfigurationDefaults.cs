using System;
using Exceptionless.Models;

namespace Exceptionless.Enrichments.Default {
    public class ConfigurationDefaults : IEventEnrichment {
        /// <summary>
        /// Enrich the event with additional information.
        /// </summary>
        /// <param name="context">Context information.</param>
        /// <param name="ev">Event to enrich.</param>
        public void Enrich(EventEnrichmentContext context, Event ev) {
            foreach (string tag in context.Client.Configuration.DefaultTags)
                ev.Tags.Add(tag);

            foreach (var data in context.Client.Configuration.DefaultData)
                ev.Data[data.Key] = data.Value;
        }
    }
}