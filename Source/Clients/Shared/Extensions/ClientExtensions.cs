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
            exception.ToExceptionless(client: client).Submit();
        }

        /// <summary>
        /// Creates an exception event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="exception">The exception.</param>
        public static void CreateException(this ExceptionlessClient client, Exception exception) {
            exception.ToExceptionless(client: client).Submit();
        }

        /// <summary>
        /// Submits a log message event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="message">The log message.</param>
        public static void SubmitLog(this ExceptionlessClient client, string message) {
            client.CreateEvent().SetType(Event.KnownTypes.Log).SetMessage(message).Submit();
        }

        /// <summary>
        /// Submits a log message event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="source">The log source.</param>
        /// <param name="message">The log message.</param>
        public static void SubmitLog(this ExceptionlessClient client, string source, string message) {
            client.CreateEvent().SetType(Event.KnownTypes.Log).SetSource(source).SetMessage(message).Submit();
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
            return client.CreateEvent().SetType(Event.KnownTypes.Log).SetSource(source).SetMessage(message);
        }

        /// <summary>
        /// Submits a feature usage event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="feature">The name of the feature that was used.</param>
        public static void SubmitFeatureUsage(this ExceptionlessClient client, string feature) {
            client.CreateEvent().SetType(Event.KnownTypes.FeatureUsage).SetSource(feature).Submit();
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
        /// Submits a resource not found event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="resource">The name of the resource that was not found.</param>
        public static void SubmitNotFound(this ExceptionlessClient client, string resource) {
            client.CreateEvent().SetType(Event.KnownTypes.NotFound).SetSource(resource).Submit();
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
        /// Submits a session start event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="sessionId">The session id.</param>
        public static void SubmitSessionStart(this ExceptionlessClient client, string sessionId) {
            client.CreateEvent().SetType(Event.KnownTypes.SessionStart).SetSessionId(sessionId).Submit();
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
        /// Submits a session end event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="sessionId">The session id.</param>
        public static void SubmitSessionEnd(this ExceptionlessClient client, string sessionId) {
            client.CreateEvent().SetType(Event.KnownTypes.SessionEnd).SetSessionId(sessionId).Submit();
        }

        /// <summary>
        /// Creates a session end event.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <param name="sessionId">The session id.</param>
        public static EventBuilder CreateSessionEnd(this ExceptionlessClient client, string sessionId) {
            return client.CreateEvent().SetType(Event.KnownTypes.SessionEnd).SetSessionId(sessionId);
        }
    }
}
