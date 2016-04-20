using System;
using System.Text;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Models.Data {
    public class Module : IData {
        public Module() {
            Data = new DataDictionary();
        }

        public int ModuleId { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public bool IsEntry { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public DataDictionary Data { get; set; }

        public override string ToString() {
            var sb = new StringBuilder();
            sb.Append(Name);
            sb.Append(", Version=");
            sb.Append(Version);
            if (Data.ContainsKey("PublicKeyToken"))
                sb.Append(", PublicKeyToken=").Append(Data["PublicKeyToken"]);

            return sb.ToString();
        }

        protected bool Equals(Module other) {
            return ModuleId == other.ModuleId && string.Equals(Name, other.Name) && string.Equals(Version, other.Version) && IsEntry == other.IsEntry && CreatedDate.Equals(other.CreatedDate) && ModifiedDate.Equals(other.ModifiedDate) && Equals(Data, other.Data);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((Module)obj);
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = ModuleId;
                hashCode = (hashCode * 397) ^ (Name?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (Version?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ IsEntry.GetHashCode();
                hashCode = (hashCode * 397) ^ CreatedDate.GetHashCode();
                hashCode = (hashCode * 397) ^ ModifiedDate.GetHashCode();
                hashCode = (hashCode * 397) ^ (Data?.GetCollectionHashCode() ?? 0);
                return hashCode;
            }
        }
    }
}