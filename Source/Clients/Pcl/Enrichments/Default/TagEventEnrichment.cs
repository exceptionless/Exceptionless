using System;
using Exceptionless.Models;

namespace Exceptionless.Enrichments.Default {
    public class TagEventEnrichment : IEventEnrichment {
        /// <summary>
        /// Enrich the event with additional information.
        /// </summary>
        /// <param name="context">Context information.</param>
        /// <param name="ev">Event to enrich.</param>
        public void Enrich(EventEnrichmentContext context, Event ev) {
            foreach (string tag in context.Client.Tags)
                ev.Tags.Add(tag);

#if !SILVERLIGHT
            ExceptionlessSection settings = ClientConfigurationReader.GetApplicationConfiguration(context.Client);
            if (settings == null)
                return;

            foreach (NameValueConfigurationElement cf in settings.ExtendedData) {
                if (!String.IsNullOrEmpty(cf.Name))
                    ev.Data[cf.Name] = cf.Value;
            }

            foreach (string tag in settings.Tags.SplitAndTrim(',')) {
                if (!String.IsNullOrEmpty(tag))
                    ev.Tags.Add(tag);
            }
#endif
        }
    }
}