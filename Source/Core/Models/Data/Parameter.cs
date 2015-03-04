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
    }
}