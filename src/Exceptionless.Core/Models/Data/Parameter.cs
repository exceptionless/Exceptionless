using System;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Models.Data {
    public class Parameter : IData {
        public Parameter() {
            Data = new DataDictionary();
            GenericArguments = new GenericArguments();
        }

        public string Name { get; set; }
        public string Type { get; set; }
        public string TypeNamespace { get; set; }

        public DataDictionary Data { get; set; }
        public GenericArguments GenericArguments { get; set; }

        protected bool Equals(Parameter other) {
            return String.Equals(Name, other.Name) && String.Equals(Type, other.Type) && String.Equals(TypeNamespace, other.TypeNamespace) && Equals(Data, other.Data) && GenericArguments.CollectionEquals(other.GenericArguments);
        }

        public override bool Equals(object obj) {
            if (obj is null)
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((Parameter)obj);
        }

        public override int GetHashCode() {
            unchecked {
                int hashCode = Name?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ (Type?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (TypeNamespace?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (Data?.GetCollectionHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (GenericArguments?.GetCollectionHashCode() ?? 0);
                return hashCode;
            }
        }
    }
}