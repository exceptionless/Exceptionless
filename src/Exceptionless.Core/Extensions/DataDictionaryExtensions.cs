using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Extensions {
    public static class DataDictionaryExtensions {
        public static T GetValue<T>(this DataDictionary extendedData, string key) {
            if (!extendedData.ContainsKey(key))
                throw new KeyNotFoundException($"Key \"{key}\" not found in the dictionary.");

            object data = extendedData[key];
            if (data is T)
                return (T)data;

            if (data is JObject) {
                try {
                    return ((JObject)data).ToObject<T>();
                } catch {}
            }

            string json = data as string;
            if (json.IsJson()) {
                try {
                    return JsonConvert.DeserializeObject<T>(json);
                } catch {}
            }

            try {
                return data.ToType<T>();
            } catch {}

            return default;
        }

        public static void RemoveSensitiveData(this DataDictionary extendedData) {
            var removeKeys = extendedData.Keys.Where(k => k.StartsWith("-")).ToArray();
            foreach (var key in removeKeys)
                extendedData.Remove(key);
        }
    }
}