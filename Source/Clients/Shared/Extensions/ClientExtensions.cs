using System;
using Exceptionless.Models;

namespace Exceptionless {
    public static class ClientExtensions {
        /// <summary>
        /// Submits an unhandled exception event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="exception">The unhandled exception.</param>
        public static void SubmitUnhandledException(this ExceptionlessClient client, Exception exception) {
            var builder = exception.ToExceptionless(client: client);
            builder.EnrichmentContextData.MarkAsUnhandledError();
            builder.Submit();
        }

        /// <summary>
        /// Submits an exception event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="exception">The exception.</param>
        public static void SubmitException(this ExceptionlessClient client, Exception exception) {
            client.CreateException(exception).Submit();
        }

        /// <summary>
        /// Creates an exception event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="exception">The exception.</param>
        public static EventBuilder CreateException(this ExceptionlessClient client, Exception exception) {
            return exception.ToExceptionless(client: client);
        }

        /// <summary>
        /// Submits a log message event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="message">The log message.</param>
        public static void SubmitLog(this ExceptionlessClient client, string message) {
            client.CreateLog(message).Submit();
        }

        /// <summary>
        /// Submits a log message event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="source">The log source.</param>
        /// <param name="message">The log message.</param>
        public static void SubmitLog(this ExceptionlessClient client, string source, string message) {
            client.CreateLog(source, message).Submit();
        }

        /// <summary>
        /// Submits a log message event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="source">The log source.</param>
        /// <param name="level">The log level.</param>
        /// <param name="message">The log message.</param>
        public static void SubmitLog(this ExceptionlessClient client, string source, string message, string level) {
            client.CreateLog(source, message, level).Submit();
        }

        /// <summary>
        /// Creates a log message event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="message">The log message.</param>
        public static EventBuilder CreateLog(this ExceptionlessClient client, string message) {
            return client.CreateEvent().SetType(Event.KnownTypes.Log).SetMessage(message);
        }

        /// <summary>
        /// Creates a log message event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="source">The log source.</param>
        /// <param name="message">The log message.</param>
        public static EventBuilder CreateLog(this ExceptionlessClient client, string source, string message) {
            return client.CreateLog(message).SetSource(source);
        }

        /// <summary>
        /// Creates a log message event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="source">The log source.</param>
        /// <param name="message">The log message.</param>
        /// <param name="level">The log level.</param>
        public static EventBuilder CreateLog(this ExceptionlessClient client, string source, string message, string level) {
            var builder = client.CreateLog(source, message);

            if (!String.IsNullOrWhiteSpace(level))
                builder.AddObject(level.Trim(), Event.KnownDataKeys.Level);

            return builder;
        }

        /// <summary>
        /// Creates a feature usage event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="feature">The name of the feature that was used.</param>
        public static EventBuilder CreateFeatureUsage(this ExceptionlessClient client, string feature) {
            return client.CreateEvent().SetType(Event.KnownTypes.FeatureUsage).SetSource(feature);
        }

        /// <summary>
        /// Submits a feature usage event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="feature">The name of the feature that was used.</param>
        public static void SubmitFeatureUsage(this ExceptionlessClient client, string feature) {
            client.CreateFeatureUsage(feature).Submit();
        }

        /// <summary>
        /// Creates a resource not found event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="resource">The name of the resource that was not found.</param>
        public static EventBuilder CreateNotFound(this ExceptionlessClient client, string resource) {
            return client.CreateEvent().SetType(Event.KnownTypes.NotFound).SetSource(resource);
        }

        /// <summary>
        /// Submits a resource not found event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="resource">The name of the resource that was not found.</param>
        public static void SubmitNotFound(this ExceptionlessClient client, string resource) {
            client.CreateNotFound(resource).Submit();
        }

        /// <summary>
        /// Creates a session start event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="sessionId">The session id.</param>
        public static EventBuilder CreateSessionStart(this ExceptionlessClient client, string sessionId) {
            return client.CreateEvent().SetType(Event.KnownTypes.SessionStart).SetSessionId(sessionId);
        }
        
        /// <summary>
        /// Submits a session start event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="sessionId">The session id.</param>
        public static void SubmitSessionStart(this ExceptionlessClient client, string sessionId) {
            client.CreateSessionStart(sessionId).Submit();
        }

        /// <summary>
        /// Creates a session end event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="sessionId">The session id.</param>
        public static EventBuilder CreateSessionEnd(this ExceptionlessClient client, string sessionId) {
            return client.CreateEvent().SetType(Event.KnownTypes.SessionEnd).SetSessionId(sessionId);
        }
        
        /// <summary>
        /// Submits a session end event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="sessionId">The session id.</param>
        public static void SubmitSessionEnd(this ExceptionlessClient client, string sessionId) {
            client.CreateSessionEnd(sessionId).Submit();
        }
    }
}
