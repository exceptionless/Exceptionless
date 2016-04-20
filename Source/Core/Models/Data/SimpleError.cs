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

        protected bool Equals(SimpleError other) {
            return string.Equals(Message, other.Message) && string.Equals(Type, other.Type) && string.Equals(StackTrace, other.StackTrace) && Equals(Data, other.Data) && Equals(Inner, other.Inner);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((SimpleError)obj);
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = Message == null ? 0 : Message.GetHashCode();
                hashCode = (hashCode * 397) ^ (Type == null ? 0 : Type.GetHashCode());
                hashCode = (hashCode * 397) ^ (StackTrace == null ? 0 : StackTrace.GetHashCode());
                hashCode = (hashCode * 397) ^ (Data == null ? 0 : Data.GetCollectionHashCode());
                hashCode = (hashCode * 397) ^ (Inner == null ? 0 : Inner.GetHashCode());
                return hashCode;
            }
        }

        public static class KnownDataKeys {
            public const string ExtraProperties = "@ext";
        }
    }
}