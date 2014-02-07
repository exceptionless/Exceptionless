using System;
using System.Reflection;
using System.Text;

#if !EMBEDDED
namespace CodeSmith.Core.Extensions {
    public
#else
namespace Exceptionless.Extensions {
    internal
#endif
    static class ExceptionExtensions {
        public static string GetAllMessages(this Exception exception) {
            return exception.GetAllMessages(false);
        }

        public static string GetAllMessages(this Exception exception, bool includeStackTrace) {
            var builder = new StringBuilder();

#if PFX_LEGACY_3_5 || SILVERLIGHT
            Exception current = exception;
#else
            Exception current = exception is AggregateException
                ? ((AggregateException)exception).GetInnerException()
                : exception;
#endif
            while (current != null) {
                string message = includeStackTrace ? current.FormatMessageWithStackTrace() : current.Message;
                builder.Append(message);

                if (current.InnerException != null)
                    builder.Append(" --> ");
                current = current.InnerException;
            }

            return builder.ToString();
        }

#if !PFX_LEGACY_3_5 && !SILVERLIGHT
        /// <summary>
        /// Gets the exception that is wrapped by an AggregateException.
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public static Exception GetInnerException(this AggregateException exception) {
            if (exception == null || exception.InnerException == null)
                return null;

            // Mark all of the exceptions as handled.
            exception.Handle(e => true);
            exception = exception.Flatten();

            if (exception.InnerException is TargetInvocationException && exception.InnerException.InnerException != null)
                return exception.InnerException.InnerException;

            if (exception.InnerException is ApplicationException)
                return exception.InnerException.InnerException is ApplicationException
                    ? exception.InnerException.InnerException
                    : exception.InnerException;

            return exception.InnerException;
        }
#endif

        /// <summary>
        /// Formats an exception with the stack trace included.
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public static string FormatMessageWithStackTrace(this Exception exception) {
            return String.Format("{0}\r\nStack Trace:\r\n{1}{2}", exception.Message, exception.StackTrace, Environment.NewLine);
        }
    }
}