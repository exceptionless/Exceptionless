using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Utility;

namespace Exceptionless.Enrichments.Default {
    public class TraceLogEnrichment : IEventEnrichment {
        public const string MaxEntriesToIncludeKey = "MaxEntriesToIncludeKey";
        public const int DefaultMaxEntriesToInclude = 10;

        /// <summary>
        /// Enrich the event with additional information.
        /// </summary>
        /// <param name="context">Context information.</param>
        /// <param name="ev">Event to enrich.</param>
        public void Enrich(EventEnrichmentContext context, Event ev) {
            try {
                int maxEntriesToInclude = context.Client.Configuration.Settings.GetInt32(MaxEntriesToIncludeKey, DefaultMaxEntriesToInclude);
                if (maxEntriesToInclude > 0)
                    AddRecentTraceLogEntries(ev, maxEntriesToInclude);
            } catch (Exception ex) {
                context.Log.FormattedError(typeof(TraceLogEnrichment), ex, "Error adding trace information: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Adds the trace info as extended data to the event.
        /// </summary>
        /// <param name="ev">The event model.</param>
        /// <param name="maxEntriesToInclude"></param>
        public static void AddRecentTraceLogEntries(Event ev, int maxEntriesToInclude) {
            if (ev.Data.ContainsKey(Event.KnownDataKeys.TraceLog))
                return;

            ExceptionlessTraceListener traceListener = Trace.Listeners
                .OfType<ExceptionlessTraceListener>()
                .FirstOrDefault();

            if (traceListener == null)
                return;

            List<string> logEntries = traceListener.GetLogEntries(maxEntriesToInclude);
            if (logEntries.Count > 0)
                ev.Data.Add(Event.KnownDataKeys.TraceLog, logEntries);
        }
    }
}