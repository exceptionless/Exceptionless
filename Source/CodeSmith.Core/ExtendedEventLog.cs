using System;
using System.Diagnostics;

namespace CodeSmith.Core
{
    public class ExtendedEventLog : EventLog
    {
        #region Default Singleton
        private const string DEFAULT_LOG_NAME = "CodeSmith";
        private const string DEFAULT_EVENT_SOURCE = "CodeSmith";

        public static ExtendedEventLog Default
        {
            get { return Nested.Default; }
        }

        private class Nested
        {
            static Nested()
            { }

            internal readonly static ExtendedEventLog Default = new ExtendedEventLog(DEFAULT_LOG_NAME, ".", DEFAULT_EVENT_SOURCE);
        }
        #endregion

        #region Constructors
        public ExtendedEventLog()
            : base()
        {
        }

        public ExtendedEventLog(string logName)
            : base(logName)
        {
        }

        public ExtendedEventLog(string logName, string machineName)
            : base(logName, machineName)
        {
        }

        public ExtendedEventLog(string logName, string machineName, string source)
            : base(logName, machineName, source)
        {
        }
        #endregion

        public void WriteException(Exception e)
        {
            this.WriteEntry(e.ToString(), EventLogEntryType.Error);
        }
    }
}
