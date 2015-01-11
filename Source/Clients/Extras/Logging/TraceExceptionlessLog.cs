#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Logging {
    public class TraceExceptionlessLog : IExceptionlessLog {
        public LogLevel MinimumLogLevel { get; set; }

        public void Error(string message, string source = null, Exception exception = null) {
            if (LogLevel.Error >= MinimumLogLevel)
                System.Diagnostics.Trace.WriteLine(message);
        }

        public void Info(string message, string source = null) {
            if (LogLevel.Info >= MinimumLogLevel)
                System.Diagnostics.Trace.WriteLine(message);
        }

        public void Debug(string message, string source = null) {
            if (LogLevel.Debug >= MinimumLogLevel)
                System.Diagnostics.Trace.WriteLine(message);
        }

        public void Warn(string message, string source = null) {
            if (LogLevel.Warn >= MinimumLogLevel)
                System.Diagnostics.Trace.WriteLine(message);
        }

        public void Trace(string message, string source = null) {
            if (LogLevel.Trace >= MinimumLogLevel)
                System.Diagnostics.Trace.WriteLine(message);
        }

        public void Flush() { }
    }
}