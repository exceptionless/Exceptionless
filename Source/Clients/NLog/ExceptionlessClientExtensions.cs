using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace Exceptionless.NLog {
    public static class ExceptionlessClientExtensions {
        public static EventBuilder CreateFromLogEvent(this ExceptionlessClient client, LogEventInfo ev) {
            var builder = ev.Exception != null ? client.CreateException(ev.Exception) : client.CreateLog(ev.LoggerName, ev.FormattedMessage, ev.Level.Name);
            builder.Target.Date = ev.TimeStamp;

            if (!String.IsNullOrWhiteSpace(ev.FormattedMessage))
                builder.SetMessage(ev.FormattedMessage);

            if (ev.Exception != null)
                builder.SetSource(ev.LoggerName);

            foreach (var p in ev.Properties.Where(kvp => !_ignoredEventProperties.Contains(kvp.Key.ToString(), StringComparer.OrdinalIgnoreCase)))
                builder.AddObject(p.Value, p.Key.ToString());

            return builder;
        }

        public static void SubmitFromLogEvent(this ExceptionlessClient client, LogEventInfo ev) {
            CreateFromLogEvent(client, ev).Submit();
        }

        private static readonly List<string> _ignoredEventProperties = new List<string> {
            "CallerFilePath",
            "CallerMemberName",
            "CallerLineNumber"
        };
    }
}
