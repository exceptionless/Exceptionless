using System;

namespace Exceptionless.Core.Models.Data {
    public class SimpleError : IData {
        public SimpleError() {
            Data = new DataDictionary();
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
        /// The stack trace for the error.
        /// </summary>
        public string StackTrace { get; set; }

        /// <summary>
        /// Extended data entries for this error.
        /// </summary>
        public DataDictionary Data { get; set; }

        /// <summary>
        /// An inner (nested) error.
        /// </summary>
        public SimpleError Inner { get; set; }

        public static class KnownDataKeys {
            public const string ExtraProperties = "@ext";
        }
    }
}