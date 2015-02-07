using System;
using NLog;

namespace Exceptionless.NLog {
    public static class ExceptionlessClientExtensions {
        public static EventBuilder CreateFromLogEvent(this ExceptionlessClient client, LogEventInfo ev) {
            var builder = ev.Exception != null ? client.CreateException(ev.Exception) : client.CreateLog(ev.FormattedMessage);

            builder.Target.Date = ev.TimeStamp;
            builder.SetSource(ev.LoggerName);
            if (ev.Exception == null)
                builder.SetLevel(ev.Level.Name);

            foreach (var p in ev.Properties)
                builder.AddObject(p.Value, p.Key.ToString());

            return builder;
        }

        public static void SubmitFromLogEvent(this ExceptionlessClient client, LogEventInfo ev) {
            CreateFromLogEvent(client, ev).Submit();
        }
    }
}
