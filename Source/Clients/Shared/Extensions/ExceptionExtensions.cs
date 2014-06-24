using System;
using System.Linq;
using Exceptionless.Enrichments;

namespace Exceptionless {
    public static class ExceptionExtensions {
        /// <summary>
        /// Creates a builder object for constructing error reports in a fluent api.
        /// </summary>
        /// <param name="exception">The exception.</param>
        /// <param name="enrichmentContextData">
        /// Any contextual data objects to be used by Exceptionless plugins to gather default
        /// information for inclusion in the report information.
        /// </param>
        /// <param name="client">
        /// The ExceptionlessClient instance used for configuration. If a client is not specified, it will use
        /// ExceptionlessClient.Default.
        /// </param>
        /// <returns></returns>
        public static EventBuilder ToExceptionless(this Exception exception, ContextData enrichmentContextData = null, ExceptionlessClient client = null) {
            if (client == null)
                client = ExceptionlessClient.Default;

            if (enrichmentContextData == null)
                enrichmentContextData = new ContextData();

            enrichmentContextData.SetException(exception);

            return client.CreateEvent(enrichmentContextData);
        }
    }
}

namespace Exceptionless.Extensions {
    public static class ExceptionExtensions {
        public static Exception GetInnermostException(this Exception exception) {
            if (exception == null)
                return null;

            Exception current = exception;
            while (current.InnerException != null)
                current = current.InnerException;

            return current;
        }

        public static string GetMessage(this Exception exception) {
            if (exception == null)
                return String.Empty;

            if (exception is AggregateException)
                return String.Join(Environment.NewLine, ((AggregateException)exception).InnerExceptions.Where(ex => !String.IsNullOrEmpty(ex.Message)).Select(ex => ex.Message));

            return exception.Message;
        }
    }
}