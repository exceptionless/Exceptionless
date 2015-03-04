using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Models {
    public class DataDictionary : Dictionary<string, object> {
        public DataDictionary() : base(StringComparer.OrdinalIgnoreCase) {}

        public DataDictionary(IEnumerable<KeyValuePair<string, object>> values) : base(StringComparer.OrdinalIgnoreCase) {
            foreach (var kvp in values)
                Add(kvp.Key, kvp.Value);
        }

        public object GetValueOrDefault(string key) {
            object value;
            return TryGetValue(key, out value) ? value : null;
        }

        public object GetValueOrDefault(string key, object defaultValue) {
            object value;
            return TryGetValue(key, out value) ? value : defaultValue;
        }

        public object GetValueOrDefault(string key, Func<object> defaultValueProvider) {
            object value;
            return TryGetValue(key, out value) ? value : defaultValueProvider();
        }

        public string GetString(string name) {
            return GetString(name, String.Empty);
        }

        public string GetString(string name, string @default) {
            object value;

            if (!TryGetValue(name, out value))
                return @default;

            if (value is string)
                return (string)value;
                
            return String.Empty;
        }
    }
}