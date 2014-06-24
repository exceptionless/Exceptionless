using System;
using Exceptionless.Enrichments.Default;

namespace Exceptionless {
    public static class EventBuilderExtensions {
        /// <summary>
        /// Adds the recent trace log entries to the event.
        /// </summary>
        /// <param name="builder">The event builder object.</param>
        public static EventBuilder AddRecentTraceLogEntries(this EventBuilder builder) {
            TraceLogEnrichment.AddRecentTraceLogEntries(builder.Target);
            return builder;
        }
    }
}