using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Extensions;
using Pcl;

namespace Exceptionless.Serializer {
    public class DefaultSerializer : IJsonSerializer {
        public string Serialize(object model, string[] exclusions = null) {
            return SimpleJson.SerializeObject(model);
        }

        public T Deserialize<T>(string json) {
            return SimpleJson.DeserializeObject<T>(json);
        }

        internal class JsonSerializerWithExclusionsStrategy : IJsonSerializerStrategy {
            private readonly IJsonSerializerStrategy _pocoSerializerStrategy = new PocoJsonSerializerStrategy();
            private readonly string[] _exclusions;

            internal JsonSerializerWithExclusionsStrategy(string[] exclusions) {
                _exclusions = exclusions;
            }

            public bool TrySerializeNonPrimitiveObject(object input, out object output) {
                bool success = _pocoSerializerStrategy.TrySerializeNonPrimitiveObject(input, out output);

                if (!success)
                    return false;

                var dict = output as IDictionary<string, object>;
                if (dict == null)
                    return true;

                RemoveExclusions(dict);

                return true;
            }

            public object DeserializeObject(object value, Type type) {
                return _pocoSerializerStrategy.DeserializeObject(value, type);
            }

            private void RemoveExclusions(IDictionary<string, object> value) {
                foreach (string key in value.Keys.ToList()) {
                    if (key.AnyWildcardMatches(_exclusions, true))
                        value.Remove(key);

                    if (value[key] is JsonObject)
                        RemoveExclusions(value);
                }
            }
        }
    }
}
