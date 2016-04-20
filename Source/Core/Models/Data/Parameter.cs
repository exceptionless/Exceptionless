using System;

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
            return string.Equals(Name, other.Name) && string.Equals(Type, other.Type) && string.Equals(TypeNamespace, other.TypeNamespace) && Equals(Data, other.Data) && GenericArguments.CollectionEquals(other.GenericArguments);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((Parameter)obj);
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = Name == null ? 0 : Name.GetHashCode();
                hashCode = (hashCode * 397) ^ (Type == null ? 0 : Type.GetHashCode());
                hashCode = (hashCode * 397) ^ (TypeNamespace == null ? 0 : TypeNamespace.GetHashCode());
                hashCode = (hashCode * 397) ^ (Data == null ? 0 : Data.GetCollectionHashCode());
                hashCode = (hashCode * 397) ^ (GenericArguments == null ? 0 : GenericArguments.GetCollectionHashCode());
                return hashCode;
            }
        }
    }
}