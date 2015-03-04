using System;

namespace Exceptionless.Core.Models.Data {
    public class InnerError : IData {
        public InnerError() {
            Data = new DataDictionary();
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
        public DataDictionary Data { get; set; }

        /// <summary>
        /// An inner (nested) error.
        /// </summary>
        public InnerError Inner { get; set; }

        /// <summary>
        /// The stack trace for the error.
        /// </summary>
        public StackFrameCollection StackTrace { get; set; }

        /// <summary>
        /// The target method.
        /// </summary>
        public Method TargetMethod { get; set; }
    }
}