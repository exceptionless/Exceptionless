using System;
using Exceptionless.Models;

namespace Exceptionless.Enrichments.Default {
    public class ConfigurationDefaultsEnrichment : IEventEnrichment {
        public void Enrich(EventEnrichmentContext context, Event ev) {
            foreach (string tag in context.Client.Configuration.DefaultTags)
                ev.Tags.Add(tag);

            if (ev.Type != Event.KnownTypes.Error)
                return;

            foreach (var data in context.Client.Configuration.DefaultData)
                ev.Data[data.Key] = data.Value;
        }
    }
}