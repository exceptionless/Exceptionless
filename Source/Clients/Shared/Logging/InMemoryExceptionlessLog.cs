using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Exceptionless.Logging {
    public class InMemoryExceptionlessLog : IExceptionlessLog {
        public const int DEFAULT_MAX_ENTRIES_TO_STORE = 250;

        private readonly Queue<LogEntry> _innerList;
        private int _maxEntriesToStore;

        public InMemoryExceptionlessLog() {
            _innerList = new Queue<LogEntry>();
            MaxEntriesToStore = DEFAULT_MAX_ENTRIES_TO_STORE;
        }

        public InMemoryExceptionlessLog(int maxEntriesToStore) : this() {
            MaxEntriesToStore = maxEntriesToStore;
        }

        public InMemoryExceptionlessLog(string maxEntriesToStore)
            : this() {
            int value;
            if (Int32.TryParse(maxEntriesToStore, out value))
                MaxEntriesToStore = value;
        }

        public int MaxEntriesToStore {
            get { return _maxEntriesToStore; }
            set {
                _maxEntriesToStore = value;
                
                if (_maxEntriesToStore <= 0)
                    InnerList.Clear();
            }
        }

        public override string ToString() {
            var output = new StringBuilder();

            foreach (LogEntry logEntry in InnerList)
                output.Append(logEntry);

            return output.ToString();
        }

        private Queue<LogEntry> InnerList { get { return _innerList; } }

        public List<LogEntry> GetLogEntries(int entryCount = 10) {
            return new List<LogEntry>(InnerList.OrderByDescending(l => l.Date).Take(entryCount).ToArray());
        }

        public LogLevel MinimumLogLevel { get; set; }

        private void Write(LogEntry entry) {
            if (MaxEntriesToStore <= 0 || entry == null)
                return;

            if (entry.Level < MinimumLogLevel)
                return;

            entry.Date = DateTime.Now;
            InnerList.Enqueue(entry);

            while (InnerList.Count > Math.Max(0, MaxEntriesToStore))
                InnerList.Dequeue();
        }

        public void Error(string message, string source = null, Exception exception = null) {
            Write(new LogEntry { Level = LogLevel.Error, Source = source, Message = message, Exception = exception });
        }

        public void Info(string message, string source = null) {
            Write(new LogEntry { Level = LogLevel.Info, Source = source, Message = message });
        }

        public void Debug(string message, string source = null) {
            Write(new LogEntry { Level = LogLevel.Debug, Source = source, Message = message });
        }

        public void Warn(string message, string source = null) {
            Write(new LogEntry { Level = LogLevel.Warn, Source = source, Message = message });
        }

        public void Trace(string message, string source = null) {
            Write(new LogEntry { Level = LogLevel.Trace, Source = source, Message = message });
        }

        public void Flush() {}

        public class LogEntry {
            public DateTime Date { get; set; }
            public LogLevel Level { get; set; }
            public string Source { get; set; }
            public string Message { get; set; }
            public Exception Exception { get; set; }

            public override string ToString() {
                var sb = new StringBuilder();
                sb.Append(Date.ToString("mm:ss")).Append(' ');
                sb.Append(Level.ToString().PadRight(5)).Append(' ');
                sb.Append(Source).Append(" - ");
                sb.Append(Message);
                return sb.ToString();
            }
        }
    }
}