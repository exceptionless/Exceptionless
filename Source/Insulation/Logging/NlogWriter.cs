using System;
using Foundatio.Logging;

namespace Exceptionless.Insulation.Logging {
    /// <summary>
    /// NLog log writer adapter
    /// </summary>
    public static class NLogWriter {
        /// <summary>
        /// Writes the specified LogData to NLog.
        /// </summary>
        /// <param name="logData">The log data.</param>
        public static void WriteLog(LogData logData) {
            var logEvent = logData.ToLogEvent();
            var name = logData.Logger ?? typeof(NLogWriter).FullName;

            var logger = global::NLog.LogManager.GetLogger(name);
            logger.Log(logEvent);
        }

        /// <summary>
        /// Converts the LogData to LogEventInfo.
        /// </summary>
        /// <param name="logData">The log data.</param>
        /// <returns></returns>
        public static global::NLog.LogEventInfo ToLogEvent(this LogData logData) {
            var logEvent = new global::NLog.LogEventInfo();
            logEvent.TimeStamp = DateTime.Now;
            logEvent.Level = logData.LogLevel.ToLogLevel();
            logEvent.LoggerName = logData.Logger;
            logEvent.Exception = logData.Exception;
            logEvent.FormatProvider = logData.FormatProvider;
            logEvent.Message = logData.Message;
            logEvent.Parameters = logData.Parameters;

            if (logData.Properties != null)
                foreach (var property in logData.Properties)
                    logEvent.Properties[property.Key] = property.Value;

            logEvent.Properties["CallerMemberName"] = logData.MemberName;
            logEvent.Properties["CallerFilePath"] = logData.FilePath;
            logEvent.Properties["CallerLineNumber"] = logData.LineNumber;

            return logEvent;
        }

        /// <summary>
        /// Converts the LogLevel to NLog.LogLevel
        /// </summary>
        /// <param name="logLevel">The log level.</param>
        /// <returns></returns>
        public static global::NLog.LogLevel ToLogLevel(this LogLevel logLevel) {
            switch (logLevel) {
                case LogLevel.Fatal:
                    return global::NLog.LogLevel.Fatal;
                case LogLevel.Error:
                    return global::NLog.LogLevel.Error;
                case LogLevel.Warn:
                    return global::NLog.LogLevel.Warn;
                case LogLevel.Info:
                    return global::NLog.LogLevel.Info;
                case LogLevel.Trace:
                    return global::NLog.LogLevel.Trace;
            }

            return global::NLog.LogLevel.Debug;
        }
    }

    /// <summary>
    /// NLog writer adapter
    /// </summary>
    public class NLogAdapter : ILogWriter {
        /// <summary>
        /// Writes the log.
        /// </summary>
        /// <param name="logData">The log data.</param>
        public void WriteLog(LogData logData) {
            NLogWriter.WriteLog(logData);
        }
    }
}