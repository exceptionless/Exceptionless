using System;
using System.Diagnostics;
using System.Linq;
using Exceptionless.Diagnostics;
using Exceptionless.Enrichments.Default;

namespace Exceptionless {
    public static class EventBuilderExtensions {
        /// <summary>
        /// Adds the recent trace log entries to the event.
        /// </summary>
        /// <param name="builder">The event builder object.</param>
        /// <param name="listener">The listener.</param>
        /// <param name="maxEntriesToInclude"></param>
        public static EventBuilder AddRecentTraceLogEntries(this EventBuilder builder, ExceptionlessTraceListener listener = null, int maxEntriesToInclude = TraceLogEnrichment.DefaultMaxEntriesToInclude) {
            TraceLogEnrichment.AddRecentTraceLogEntries(builder.Target, listener, maxEntriesToInclude);
            return builder;
        }
    }
}