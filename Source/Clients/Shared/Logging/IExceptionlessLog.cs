#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Logging {
    public interface IExceptionlessLog {
        LogLevel MinimumLogLevel { get; set; }
        void Error(string message, string source = null, Exception exception = null);
        void Info(string message, string source = null);
        void Debug(string message, string source = null);
        void Warn(string message, string source = null);
        void Trace(string message, string source = null);
        void Flush();
    }
}