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

        protected bool Equals(InnerError other) {
            return string.Equals(Message, other.Message) && string.Equals(Type, other.Type) && string.Equals(Code, other.Code) && Equals(Data, other.Data) && Equals(Inner, other.Inner) && StackTrace.CollectionEquals(other.StackTrace) && Equals(TargetMethod, other.TargetMethod);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((InnerError)obj);
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = Message == null ? 0 : Message.GetHashCode();
                hashCode = (hashCode * 397) ^ (Type == null ? 0 : Type.GetHashCode());
                hashCode = (hashCode * 397) ^ (Code == null ? 0 : Code.GetHashCode());
                hashCode = (hashCode * 397) ^ (Data == null ? 0 : Data.GetCollectionHashCode());
                hashCode = (hashCode * 397) ^ (Inner == null ? 0 : Inner.GetHashCode());
                hashCode = (hashCode * 397) ^ (StackTrace == null ? 0 : StackTrace.GetCollectionHashCode());
                hashCode = (hashCode * 397) ^ (TargetMethod == null ? 0 : TargetMethod.GetHashCode());
                return hashCode;
            }
        }
    }
}