using System;

namespace Exceptionless.Core.Models.Data {
    public class Method : IData {
        public Method() {
            Data = new DataDictionary();
            GenericArguments = new GenericArguments();
            Parameters = new ParameterCollection();
        }

        public bool IsSignatureTarget { get; set; }
        public string DeclaringNamespace { get; set; }
        public string DeclaringType { get; set; }

        public string Name { get; set; }

        public int ModuleId { get; set; }
        public DataDictionary Data { get; set; }
        public GenericArguments GenericArguments { get; set; }
        public ParameterCollection Parameters { get; set; }
    }
}