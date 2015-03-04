using System;
using Exceptionless.Core.Extensions;

namespace Exceptionless.EventMigration.Models {
    public class Method {
        public Method() {
            ExtendedData = new ExtendedDataDictionary();
            GenericArguments = new GenericArguments();
            Parameters = new ParameterCollection();
        }

        public bool IsSignatureTarget { get; set; }
        public string DeclaringNamespace { get; set; }
        public string DeclaringType { get; set; }

        public string DeclaringTypeFullName {
            get {
                if (!String.IsNullOrEmpty(DeclaringNamespace) && !String.IsNullOrEmpty(DeclaringType))
                    return String.Concat(DeclaringNamespace, ".", DeclaringType.Replace('+', '.'));

                if (!String.IsNullOrEmpty(DeclaringType))
                    return DeclaringType.Replace('+', '.');

                return String.Empty;
            }
        }

        public string Name { get; set; }

        public int ModuleId { get; set; }
        public ExtendedDataDictionary ExtendedData { get; set; }
        public GenericArguments GenericArguments { get; set; }
        public ParameterCollection Parameters { get; set; }

        public Exceptionless.Core.Models.Data.Method ToMethod() {
            var module = new Exceptionless.Core.Models.Data.Method {
                Name = Name,
                ModuleId = ModuleId,
                DeclaringNamespace = DeclaringNamespace,
                DeclaringType = DeclaringType,
                IsSignatureTarget = IsSignatureTarget
            };

            if (GenericArguments != null && GenericArguments.Count > 0)
                module.GenericArguments = GenericArguments.ToGenericArguments();

            if (Parameters != null && Parameters.Count > 0)
                module.Parameters = Parameters.ToParameters();

            if (ExtendedData != null && ExtendedData.Count > 0)
                module.Data.AddRange(ExtendedData.ToData());

            return module;
        }
    }
}