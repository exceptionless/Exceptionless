using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Exceptionless.Core.Extensions {
    public static class DictionaryExtensions {
        public static bool ContainsKeyAndValueIsNullOrEmpty(this Dictionary<string, object> map, string key) {
            return map.ContainsKey(key) && (map[key] == null || map[key].ToString().IsNullOrEmpty());
        }

        public static void ApplyIf<TKey, TValue>(this Dictionary<TKey, TValue> map1, Dictionary<TKey, TValue> map2) {
            foreach (var pair in map2)
                if (!map1.ContainsKey(pair.Key))
                    map1.Add(pair.Key, pair.Value);
        }

        public static void Add<TKey, TValue>(this Dictionary<TKey, TValue> map, IEnumerable<TKey> keys, IEnumerable<TValue> values) {
            if (keys.Count() != values.Count())
                throw new ArgumentException("Keys and values must be matching length.");

            var keyEnumerator = keys.GetEnumerator();
            var valueEnumerator = values.GetEnumerator();

            while (keyEnumerator.MoveNext() && valueEnumerator.MoveNext()) {
                if (!map.ContainsKey(keyEnumerator.Current))
                    map.Add(keyEnumerator.Current, valueEnumerator.Current);
            }
        }

        public static void AddDataIfNotEmpty(this Dictionary<string, string> dictionary, XDocument document, string elementName) {
            var element = document.Root.Element(elementName);
            if (element != null)
                dictionary.AddItemIfNotEmpty(elementName, element.Value);
        }

        public static void AddItemIfNotEmpty(this IDictionary<string, string> dictionary, string key, string value) {
            if (key == null)
                throw new ArgumentNullException("key");

            if (!String.IsNullOrEmpty(value))
                dictionary[key] = value;
        }

        /// <summary>
        /// Adds or overwrites the existing value.
        /// </summary>
        public static void AddOrUpdate<K, V>(this ConcurrentDictionary<K, V> dictionary, K key, V value) {
            dictionary.AddOrUpdate(key, value, (oldkey, oldvalue) => value);
        }

        public static IDictionary<T1, T2> Merge<T1, T2>(this IDictionary<T1, T2> first, IDictionary<T1, T2> second) {
            if (first == null)
                throw new ArgumentNullException("first");
            if (second == null)
                throw new ArgumentNullException("second");

            var merged = new Dictionary<T1, T2>();
            first.ToList().ForEach(kv => merged[kv.Key] = kv.Value);
            second.ToList().ForEach(kv => merged[kv.Key] = kv.Value);

            return merged;
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