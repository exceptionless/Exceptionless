using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Extensions;
using SimpleJson;

namespace Exceptionless.Serializer {
    public class DefaultJsonSerializer : IJsonSerializer {
        internal static IJsonSerializer Instance = new DefaultJsonSerializer();

        public string Serialize(object model, string[] exclusions = null, int maxDepth = 5) {
            var serializerStrategy = new JsonSerializerWithExclusionsStrategy(exclusions, maxDepth: maxDepth);
            return SimpleJson.SimpleJson.SerializeObject(model, serializerStrategy);
        }

        public object Deserialize(string json, Type type) {
            return SimpleJson.SimpleJson.DeserializeObject(json, type);
        }

        internal class JsonSerializerWithExclusionsStrategy : PocoJsonSerializerStrategy {
            private readonly string[] _exclusions;
            private readonly bool _excludeDefaultValues = true;
            private readonly bool _excludeEmptyCollections = true;
            private readonly int _maxDepth = -1;
            private int _currentDepth;

            internal JsonSerializerWithExclusionsStrategy(string[] exclusions, bool excludeDefaultValues = true, bool excludeEmptyCollections = true, int maxDepth = -1) {
                _exclusions = exclusions;
                _excludeDefaultValues = excludeDefaultValues;
                _excludeEmptyCollections = excludeEmptyCollections;
                _maxDepth = maxDepth;
            }

            public override bool TrySerializeNonPrimitiveObject(object input, out object output) {
                _currentDepth++;
                if (_currentDepth > _maxDepth && !input.GetType().IsValueType) {
                    output = null;
                    return true;
                }

                bool success = base.TrySerializeNonPrimitiveObject(input, out output);

                if (!success)
                    return false;

                var dict = output as IDictionary<string, object>;
                if (dict == null)
                    return true;

                RemoveExclusions(dict);

                return true;
            }

            private void RemoveExclusions(IDictionary<string, object> values) {
                var keysToRemove = new List<string>();

                foreach (string key in values.Keys.ToList()) {
                    object value = values[key];
                    // don't serialize null values
                    if (value == null) {
                        keysToRemove.Add(key);
                        continue;
                    }

                    if (key.AnyWildcardMatches(_exclusions, true)) {
                        // remove any items that are in the exclusions list
                        keysToRemove.Add(key);
                    } else if (_excludeEmptyCollections && value is ICollection && ((ICollection)value).Count == 0) {
                        // don't serialize empty collections
                        keysToRemove.Add(key);
                    } else if (_excludeEmptyCollections && ShouldCheckTypeForCount(value.GetType())) {
                        // don't serialize empty generic collections
                        int count = (int)value.GetType().GetProperty("Count").GetValue(value, null);
                        if (count == 0)
                            keysToRemove.Add(key);
                    } else if (_excludeDefaultValues && Equals(value, GetDefaultValue(value.GetType()))) {
                        // don't serialize default values
                        keysToRemove.Add(key);
                    }
                }

                foreach (string key in keysToRemove)
                    values.Remove(key);
            }

            private object GetDefaultValue(Type t) {
                if (t.IsValueType)
                    return Activator.CreateInstance(t);

                return null;
            }

            private bool ShouldCheckTypeForCount(Type type) {
                if (type == typeof(string))
                    return false;
                if (type.IsValueType)
                    return false;

                return type.GetInterfaces().FirstOrDefault(i => (i.IsGenericType) && (i.GetGenericTypeDefinition() == typeof(ICollection<>))) != null;
            }
        }
    }
}
