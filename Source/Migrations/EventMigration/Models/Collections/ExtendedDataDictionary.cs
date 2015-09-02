using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;

namespace Exceptionless.EventMigration.Models {
    public class ExtendedDataDictionary : Dictionary<string, object> {
        public const string EXCEPTION_INFO_KEY = "__ExceptionInfo";
        public const string TRACE_LOG_KEY = "TraceLog";

        public ExtendedDataDictionary() : base(StringComparer.OrdinalIgnoreCase) {}

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

        public DataDictionary ToData() {
            var dictionary = new DataDictionary();
            foreach (var item in this)
                dictionary.Add(item.Key, item.Value);
            // TODO: We should be converting strings to jobject.

            return dictionary;
        }
    }
}