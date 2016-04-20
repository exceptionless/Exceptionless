using System;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Models.Data {
    public class Error : InnerError {
        public Error() {
            Modules = new ModuleCollection();
        }

        /// <summary>
        /// Any modules that were loaded / referenced when the error occurred.
        /// </summary>
        public ModuleCollection Modules { get; set; }

        public static class KnownDataKeys {
            public const string ExtraProperties = "@ext";
            public const string TargetInfo = "@target";
        }

        protected bool Equals(Error other) {
            return base.Equals(other) && Modules.CollectionEquals(other.Modules);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((Error)obj);
        }

        public override int GetHashCode() {
            unchecked {
                return (base.GetHashCode() * 397) ^ (Modules?.GetCollectionHashCode() ?? 0);
            }
        }
    }
}
