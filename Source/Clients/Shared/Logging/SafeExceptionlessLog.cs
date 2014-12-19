#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Logging {
    public class SafeExceptionlessLog : IExceptionlessLog, IDisposable {
        private readonly IExceptionlessLog _log;
        private readonly IExceptionlessLog _fallbackLog;

        public SafeExceptionlessLog(IExceptionlessLog log, IExceptionlessLog fallbackLog = null) {
            _log = log;
            _fallbackLog = fallbackLog ?? new NullExceptionlessLog();
            MinimumLogLevel = LogLevel.Info;
        }

        private LogLevel _logLevel;
        public LogLevel MinimumLogLevel {
            get { return _logLevel; }
            set {
                _logLevel = value;
                _log.MinimumLogLevel = value;
                _fallbackLog.MinimumLogLevel = value;
            }
        }

        public void Error(string message, string source = null, Exception exception = null) {
            try {
                _log.Error(message, source, exception);
            } catch (Exception ex) {
                try {
                    _fallbackLog.Error("Error writing to log.", null, ex);
                    _fallbackLog.Error(message, source, exception);
                } catch {}
            }
        }

        public void Info(string message, string source = null) {
            try {
                _log.Info(message, source);
            } catch (Exception ex) {
                try {
                    _fallbackLog.Error("Error writing to log.", null, ex);
                    _fallbackLog.Info(message, source);
                } catch {}
            }
        }

        public void Debug(string message, string source = null) {
            try {
                _log.Debug(message, source);
            } catch (Exception ex) {
                try {
                    _fallbackLog.Error("Error writing to log.", null, ex);
                    _fallbackLog.Debug(message, source);
                } catch {}
            }
        }

        public void Warn(string message, string source = null) {
            try {
                _log.Warn(message, source);
            } catch (Exception ex) {
                try {
                    _fallbackLog.Error("Error writing to log.", null, ex);
                    _fallbackLog.Warn(message, source);
                } catch {}
            }
        }

        public void Trace(string message, string source = null) {
            try {
                _log.Trace(message, source);
            } catch (Exception ex) {
                try {
                    _fallbackLog.Error("Error writing to log.", null, ex);
                    _fallbackLog.Trace(message, source);
                } catch {}
            }
        }

        public void Flush() {
            try {
                _log.Flush();
            } catch (Exception ex) {
                try {
                    _fallbackLog.Error("Error flushing log.", null, ex);
                    _fallbackLog.Flush();
                } catch {}
            }
        }

        public void Dispose() {
            var log = _log as IDisposable;
            if (log != null) {
                try {
                    log.Dispose();
                } catch {}
            }

            var fallbackLog = _fallbackLog as IDisposable;
            if (fallbackLog != null) {
                try {
                    fallbackLog.Dispose();
                } catch {}
            }
        }
    }
}