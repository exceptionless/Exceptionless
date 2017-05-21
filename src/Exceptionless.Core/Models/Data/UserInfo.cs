using System;
using System.Diagnostics;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Models.Data {
    [DebuggerDisplay("{Identity}, {Name}")]
    public class UserInfo : IData {
        public UserInfo() {
            Data = new DataDictionary();
        }

        public UserInfo(string identity) : this() {
            if (!String.IsNullOrWhiteSpace(identity))
                Identity = identity.Trim();
        }

        public UserInfo(string identity, string name) : this(identity) {
            if (!String.IsNullOrWhiteSpace(name))
                Name = name.Trim();
        }

        /// <summary>
        /// Uniquely identifies the user.
        /// </summary>
        public string Identity { get; set; }

        /// <summary>
        /// The Friendly name of the user.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Extended data entries for this user.
        /// </summary>
        public DataDictionary Data { get; set; }

        protected bool Equals(UserInfo other) {
            return string.Equals(Identity, other.Identity) && string.Equals(Name, other.Name) && Equals(Data, other.Data);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((UserInfo)obj);
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = Identity?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ (Name?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (Data?.GetCollectionHashCode() ?? 0);
                return hashCode;
            }
        }
    }
}