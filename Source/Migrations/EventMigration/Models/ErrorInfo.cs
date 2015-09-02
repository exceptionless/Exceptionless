using System;
using Exceptionless.Core.Extensions;

namespace Exceptionless.EventMigration.Models {
    public class ErrorInfo {
        public ErrorInfo() {
            ExtendedData = new ExtendedDataDictionary();
            StackTrace = new StackFrameCollection();
        }

        /// <summary>
        /// The error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// The error type.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The error code.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Extended data entries for this error.
        /// </summary>
        public ExtendedDataDictionary ExtendedData { get; set; }

        /// <summary>
        /// An inner (nested) error.
        /// </summary>
        public ErrorInfo Inner { get; set; }

        /// <summary>
        /// The stack trace for the error.
        /// </summary>
        public StackFrameCollection StackTrace { get; set; }

        /// <summary>
        /// The target method.
        /// </summary>
        public Method TargetMethod { get; set; }

        public Exceptionless.Core.Models.Data.InnerError ToInnerError() {
            var error = new Exceptionless.Core.Models.Data.InnerError {
                Message = Message,
                Type = Type,
                Code = Code,
            };

            if (StackTrace != null && StackTrace.Count > 0)
                error.StackTrace = StackTrace.ToStackTrace();

            if (TargetMethod != null)
                error.TargetMethod = TargetMethod.ToMethod();

            if (Inner != null)
                error.Inner = Inner.ToInnerError();

            if (ExtendedData != null && ExtendedData.Count > 0)
                error.Data.AddRange(ExtendedData.ToData());

            return error;
        }
    }
}