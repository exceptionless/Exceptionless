using System;
using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless.Enrichments.Default {
    //public class TraceLogEnrichment : IEventEnrichment {
    //    /// <summary>
    //    /// Enrich the event with additional information.
    //    /// </summary>
    //    /// <param name="context">Context information.</param>
    //    /// <param name="ev">Event to enrich.</param>
    //    public void Enrich(EventEnrichmentContext context, Event ev) {
    //        try {
    //            if (context.Client.Configuration.TraceLogLimit > 0)
    //                AddRecentTraceLogEntries(ev);
    //        } catch (Exception ex) {
    //            context.Client.Log.FormattedError(typeof(TraceLogEventEnrichment), ex, "Error adding trace information: {0}", ex.Message);
    //        }
    //    }

    //    /// <summary>
    //    /// Adds the trace info as extended data to the error.
    //    /// </summary>
    //    /// <param name="ev">The error model.</param>
    //    public static void AddRecentTraceLogEntries(Event ev)
    //    {
    //        if (ev.Data.ContainsKey(Event.KnownDataKeys.TraceLog))
    //            return;

    //        ExceptionlessTraceListener traceListener = Trace.Listeners
    //            .OfType<ExceptionlessTraceListener>()
    //            .FirstOrDefault();

    //        if (traceListener == null)
    //            return;

    //        List<string> logEntries = traceListener.GetLogEntries();
    //        if (logEntries.Count > 0)
    //            ev.Data.Add(Event.KnownDataKeys.TraceLog, traceListener.GetLogEntries());
    //    }
    //}
}