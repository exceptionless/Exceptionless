#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Logging;
using NLog.Fluent;

namespace Exceptionless.Core.Utility {
    public class NLogExceptionlessLog : IExceptionlessLog {
        public void Error(string message, string source = null, Exception exception = null) {
            Log.Error().Message(message).LoggerName(source).Exception(exception).Report().Write();
        }

        public void Info(string message, string source = null) {
            Log.Info().Message(message).LoggerName(source);
        }

        public void Debug(string message, string source = null) {
            Log.Debug().Message(message).LoggerName(source);
        }

        public void Warn(string message, string source = null) {
            Log.Warn().Message(message).LoggerName(source);
        }

        public void Trace(string message, string source = null) {
            Log.Trace().Message(message).LoggerName(source);
        }

        public void Flush() {}
    }
}