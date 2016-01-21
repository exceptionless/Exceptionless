using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Exceptionless.Core.Extensions {
    public static class DictionaryExtensions {
        public static void AddItemIfNotEmpty(this IDictionary<string, string> dictionary, string key, string value) {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (!String.IsNullOrEmpty(value))
                dictionary[key] = value;
        }

        /// <summary>
        /// Adds or overwrites the existing value.
        /// </summary>
        public static void AddOrUpdate<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, TValue value) {
            dictionary.AddOrUpdate(key, value, (oldkey, oldvalue) => value);
        }
        
        public static bool ContainsKeyWithValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, params TValue[] values) {
            if (dictionary == null || values == null || values.Length == 0)
                return false;

            TValue temp;
            try {
                if (!dictionary.TryGetValue(key, out temp))
                    return false;
            } catch (ArgumentNullException) {
                return false;
            }

            return values.Any(v => v.Equals(temp));
        }
    }
}