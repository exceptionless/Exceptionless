using System;
using Exceptionless.Logging;
using NLog.Fluent;

namespace Exceptionless.NLog {
    public class NLogExceptionlessLog : IExceptionlessLog {
        // ignore and let NLog determine what should be captured.
        public LogLevel MinimumLogLevel { get; set; }

        public void Error(string message, string source = null, Exception exception = null) {
            Log.Error().Message(message).LoggerName(source).Exception(exception).Write();
        }

        public void Info(string message, string source = null) {
            Log.Info().Message(message).LoggerName(source).Write();
        }

        public void Debug(string message, string source = null) {
            Log.Debug().Message(message).LoggerName(source).Write();
        }

        public void Warn(string message, string source = null) {
            Log.Warn().Message(message).LoggerName(source).Write();
        }

        public void Trace(string message, string source = null) {
            Log.Trace().Message(message).LoggerName(source).Write();
        }

        public void Flush() { }
    }
}
