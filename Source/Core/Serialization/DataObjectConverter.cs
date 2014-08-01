using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CodeSmith.Core.Extensions;
using CodeSmith.Core.Reflection;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NLog.Fluent;

namespace Exceptionless.Serializer {
    public class DataObjectConverter<T> : CustomCreationConverter<T> where T : IData, new() {
        private readonly IDictionary<string, Type> _dataTypeRegistry = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        private static IDictionary<string, IMemberAccessor> _propertyAccessors = new Dictionary<string, IMemberAccessor>(StringComparer.OrdinalIgnoreCase);

        public DataObjectConverter(IEnumerable<KeyValuePair<string, Type>> knownDataTypes = null) {
            if (knownDataTypes != null)
                _dataTypeRegistry.AddRange(knownDataTypes);

            foreach (var prop in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public))
                _propertyAccessors.Add(prop.Name, LateBinder.GetPropertyAccessor(prop));
        }

        public void AddKnownDataType(string name, Type dataType) {
            _dataTypeRegistry.Add(name, dataType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            var target = Create(objectType);
            var json = JObject.Load(reader);

            foreach (var p in json.Properties()) {
                // first set the native properties
                string propertyName = p.Name.ToLowerFiltered(new[] { '.', '-', '_' });
                var accessor = _propertyAccessors.ContainsKey(propertyName) ? _propertyAccessors[propertyName] : null;
                if (accessor != null) {
                    accessor.SetValue(target, p.Value.ToObject(accessor.MemberType));
                    continue;
                }

                // when adding items to data, see if they are a known type and deserialize to the registered type
                if (_dataTypeRegistry.ContainsKey(p.Name)) {
                    try {
                        if (p.Value is JValue && p.Value.Type == JTokenType.String) {
                            target.Data.Add(p.Name, serializer.Deserialize(new StringReader(p.Value.ToString()), _dataTypeRegistry[p.Name]));
                        } else {
                            target.Data.Add(p.Name, p.Value.ToObject(_dataTypeRegistry[p.Name], serializer));
                        }
                        continue;
                    } catch (Exception ex) {
                        Log.Error().Exception(ex).Message("Error serializing known data type \"{0}\": {1}", p.Name, ex.Message).Write();
                    }
                }

                // add item to data as a JObject, JArray or native type.
                if (p.Value is JObject)
                    target.Data.Add(p.Name, p.Value.ToObject<JObject>());
                else if (p.Value is JArray)
                    target.Data.Add(p.Name, p.Value.ToObject<JArray>());
                else if (p.Value is JValue)
                    target.Data.Add(p.Name, ((JValue)p.Value).Value);
                else
                    target.Data.Add(p.Name, p.Value.ToString());
            }

            return target;
        }

        public override T Create(Type objectType) {
            return new T();
        }

        public override bool CanRead {
            get { return true; }
        }

        public override bool CanWrite {
            get { return false; }
        }

        public override bool CanConvert(Type objectType) {
            return objectType == typeof(T);
        }
    }
}