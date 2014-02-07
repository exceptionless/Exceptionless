using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace CodeSmith.Core.Reflection
{
    /// <summary>
    /// This exception is returned from the 
    /// CallMethod method in the server-side DataPortal
    /// and contains the exception thrown by the
    /// underlying business object method that was
    /// being invoked.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors")]
#if !SILVERLIGHT
    [Serializable]
#endif
    public class CallMethodException : Exception
    {
        private readonly string _innerStackTrace;

        /// <summary>
        /// Creates an instance of the object.
        /// </summary>
        /// <param name="message">Message text describing the exception.</param>
        /// <param name="ex">Inner exception object.</param>
        public CallMethodException(string message, Exception ex)
            : base(message, ex)
        {
            _innerStackTrace = ex.StackTrace;
        }

#if !SILVERLIGHT
        /// <summary>
        /// Creates an instance of the object for deserialization.
        /// </summary>
        /// <param name="info">Serialization info.</param>
        /// <param name="context">Serialiation context.</param>
        protected CallMethodException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _innerStackTrace = info.GetString("_innerStackTrace");
        }
#endif
        /// <summary>
        /// Get the stack trace from the original
        /// exception.
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        [SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider",
            MessageId = "System.String.Format(System.String,System.Object,System.Object,System.Object)")]
        public override string StackTrace
        {
            get { return String.Format("{0}{1}{2}", _innerStackTrace, Environment.NewLine, base.StackTrace); }
        }

#if !SILVERLIGHT
        /// <summary>
        /// Serializes the object.
        /// </summary>
        /// <param name="info">Serialization info.</param>
        /// <param name="context">Serialization context.</param>
        [SuppressMessage("Microsoft.Design", "CA1062:ValidateArgumentsOfPublicMethods")]
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("_innerStackTrace", _innerStackTrace);
        }
#endif
    }
}