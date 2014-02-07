#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Logging {
    public static class ExceptionlessLogExtensions {
        public static void Error(this IExceptionlessLog log, Type source, string message) {
            log.Error(message, GetSourceName(source));
        }

        public static void Error(this IExceptionlessLog log, Type source, Exception exception, string message) {
            log.Error(message, GetSourceName(source), exception);
        }

        public static void FormattedError(this IExceptionlessLog log, Type source, Exception exception, string format, params object[] args) {
            log.Error(String.Format(format, args), GetSourceName(source), exception);
        }

        public static void FormattedError(this IExceptionlessLog log, Type source, string format, params object[] args) {
            log.Error(String.Format(format, args), GetSourceName(source));
        }

        public static void Info(this IExceptionlessLog log, Type source, string message) {
            log.Info(message, GetSourceName(source));
        }

        public static void FormattedInfo(this IExceptionlessLog log, Type source, string format, params object[] args) {
            log.Info(String.Format(format, args), GetSourceName(source));
        }

        public static void Debug(this IExceptionlessLog log, Type source, string message) {
            log.Debug(message, GetSourceName(source));
        }

        public static void FormattedDebug(this IExceptionlessLog log, Type source, string format, params object[] args) {
            log.Debug(String.Format(format, args), GetSourceName(source));
        }

        public static void Warn(this IExceptionlessLog log, Type source, string message) {
            log.Warn(message, GetSourceName(source));
        }

        public static void FormattedWarn(this IExceptionlessLog log, Type source, string format, params object[] args) {
            log.Warn(String.Format(format, args), GetSourceName(source));
        }

        public static void Trace(this IExceptionlessLog log, Type source, string message) {
            log.Trace(message, GetSourceName(source));
        }

        public static void FormattedTrace(this IExceptionlessLog log, Type source, string format, params object[] args) {
            log.Trace(String.Format(format, args), GetSourceName(source));
        }

        public static void Error(this IExceptionlessLog log, Exception exception, string message) {
            log.Error(message, exception: exception);
        }

        public static void FormattedError(this IExceptionlessLog log, Exception exception, string format, params object[] args) {
            log.Error(String.Format(format, args), exception: exception);
        }

        public static void FormattedError(this IExceptionlessLog log, string format, params object[] args) {
            log.Error(String.Format(format, args));
        }

        public static void FormattedInfo(this IExceptionlessLog log, string format, params object[] args) {
            log.Info(String.Format(format, args));
        }

        public static void FormattedDebug(this IExceptionlessLog log, string format, params object[] args) {
            log.Debug(String.Format(format, args));
        }

        public static void FormattedWarn(this IExceptionlessLog log, string format, params object[] args) {
            log.Warn(String.Format(format, args));
        }

        private static string GetSourceName(Type type) {
            return type.Name;
        }
    }
}