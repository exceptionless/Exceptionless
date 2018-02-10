using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Models {
    public class DataDictionary : Dictionary<string, object> {
        public DataDictionary() : base(StringComparer.OrdinalIgnoreCase) {}

        public DataDictionary(IEnumerable<KeyValuePair<string, object>> values) : base(StringComparer.OrdinalIgnoreCase) {
            foreach (var kvp in values)
                Add(kvp.Key, kvp.Value);
        }

        public object GetValueOrDefault(string key) {
            return TryGetValue(key, out object value) ? value : null;
        }

        public object GetValueOrDefault(string key, object defaultValue) {
            return TryGetValue(key, out object value) ? value : defaultValue;
        }

        public object GetValueOrDefault(string key, Func<object> defaultValueProvider) {
            return TryGetValue(key, out object value) ? value : defaultValueProvider();
        }

        public string GetString(string name) {
            return GetString(name, String.Empty);
        }

        public string GetString(string name, string @default) {

            if (!TryGetValue(name, out object value))
                return @default;

            if (value is string s)
                return s;

            if (value != null) {
                try {
                    return value.ToType<string>();
                } catch { }
            }

            return String.Empty;
        }
    }
}