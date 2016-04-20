using System;

namespace Exceptionless.Core.Models.Data {
    public class StackFrame : Method {
        public string FileName { get; set; }
        public int LineNumber { get; set; }
        public int Column { get; set; }

        protected bool Equals(StackFrame other) {
            return base.Equals(other) && string.Equals(FileName, other.FileName);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((StackFrame)obj);
        }

        public override int GetHashCode() {
            unchecked {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (FileName?.GetHashCode() ?? 0);
                return hashCode;
            }
        }
    }
}