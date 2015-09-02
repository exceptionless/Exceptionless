using System;
using Exceptionless.Core.Extensions;

namespace Exceptionless.EventMigration.Models {
    public class Parameter {
        public Parameter() {
            ExtendedData = new ExtendedDataDictionary();
            GenericArguments = new GenericArguments();
        }

        public string Name { get; set; }
        public string Type { get; set; }
        public string TypeNamespace { get; set; }

        public string TypeFullName {
            get {
                if (String.IsNullOrEmpty(Name))
                    return "<UnknownType>";

                return !String.IsNullOrEmpty(TypeNamespace) ? String.Concat(TypeNamespace, ".", Type.Replace('+', '.')) : Type.Replace('+', '.');
            }
        }

        public ExtendedDataDictionary ExtendedData { get; set; }
        public GenericArguments GenericArguments { get; set; }

        public Exceptionless.Core.Models.Data.Parameter ToParameter() {
            var frame = new Exceptionless.Core.Models.Data.Parameter {
                Name = Name,
                Type = Type,
                TypeNamespace = TypeNamespace
            };

            if (GenericArguments != null && GenericArguments.Count > 0)
                frame.GenericArguments = GenericArguments.ToGenericArguments();

            if (ExtendedData != null && ExtendedData.Count > 0)
                frame.Data.AddRange(ExtendedData.ToData());

            return frame;
        }
    }
}