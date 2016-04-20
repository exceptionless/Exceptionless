﻿using System;
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
        
        public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) {
            TValue obj;
            dictionary.TryGetValue(key, out obj);
            return obj;
        }

        public static bool CollectionEquals<TValue>(this IDictionary<string, TValue> source, IDictionary<string, TValue> other) {
            if (source.Count != other.Count)
                return false;

            foreach (var key in source.Keys) {
                var sourceValue = source[key];

                TValue otherValue;
                if (!other.TryGetValue(key, out otherValue))
                    return false;

                if (sourceValue.Equals(otherValue))
                    return false;
            }

            return true;
        }


        public static int GetCollectionHashCode<TValue>(this IDictionary<string, TValue> source, IList<string> exclusions = null) {
            var assemblyQualifiedName = typeof(TValue).AssemblyQualifiedName;
            int hashCode = assemblyQualifiedName?.GetHashCode() ?? 0;

            var keyValuePairHashes = new List<int>(source.Keys.Count);

            foreach (var key in source.Keys.OrderBy(x => x)) {
                if (exclusions != null && exclusions.Contains(key))
                    continue;

                var item = source[key];
                unchecked {
                    var kvpHash = key.GetHashCode();
                    kvpHash = (kvpHash * 397) ^ item.GetHashCode();
                    keyValuePairHashes.Add(kvpHash);
                }
            }

            keyValuePairHashes.Sort();
            foreach (var kvpHash in keyValuePairHashes) {
                unchecked {
                    hashCode = (hashCode * 397) ^ kvpHash;
                }
            }

            return hashCode;
        }
    }
}