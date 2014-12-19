#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Logging {
    public class NullExceptionlessLog : IExceptionlessLog {
        public LogLevel MinimumLogLevel { get; set; }

        public void Error(string message, string source = null, Exception exception = null) {}

        public void Info(string message, string source = null) {}

        public void Debug(string message, string source = null) {}

        public void Warn(string message, string source = null) {}

        public void Trace(string message, string source = null) {}

        public void Flush() {}
    }
}