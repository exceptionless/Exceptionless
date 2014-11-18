using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            if (_propertyAccessors.Count != 0)
                return;

            foreach (var prop in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public).Where(p => p.CanWrite))
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

                if (propertyName == "data" && p.Value is JObject) {
                    foreach (var dataProp in ((JObject)p.Value).Properties())
                        AddDataEntry(serializer, dataProp, target);
                    continue;
                }

                var accessor = _propertyAccessors.ContainsKey(propertyName) ? _propertyAccessors[propertyName] : null;
                if (accessor != null) {
                    accessor.SetValue(target, p.Value.ToObject(accessor.MemberType, serializer));
                    continue;
                }

                AddDataEntry(serializer, p, target);
            }

            return target;
        }

        private void AddDataEntry(JsonSerializer serializer, JProperty p, T target) {
            if (target.Data == null)
                target.Data = new DataDictionary();

            // when adding items to data, see if they are a known type and deserialize to the registered type
            if (_dataTypeRegistry.ContainsKey(p.Name)) {
                try {
                    string dataKey = p.Name;
                    if (target.Data.ContainsKey(dataKey))
                        dataKey = "_" + dataKey;
                    if (p.Value is JValue && p.Value.Type == JTokenType.String)
                        target.Data[dataKey] = serializer.Deserialize(new StringReader(p.Value.ToString()), _dataTypeRegistry[p.Name]);
                    else
                        target.Data[dataKey] = p.Value.ToObject(_dataTypeRegistry[p.Name], serializer);
                    return;
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Error serializing known data type \"{0}\": {1}", p.Name, ex.Message).Write();
                }
            }

            // add item to data as a JObject, JArray or native type.
            if (p.Value is JObject)
                target.Data[p.Name] = p.Value.ToObject<JObject>();
            else if (p.Value is JArray)
                target.Data[p.Name] = p.Value.ToObject<JArray>();
            else if (p.Value is JValue && p.Value.Type != JTokenType.String)
                target.Data[p.Name] = ((JValue)p.Value).Value;
            else {
                string value = p.Value.ToString();
                var jsonType = value.GetJsonType();
                if (jsonType == JsonType.Object) {
                    JObject obj;
                    if (value.TryFromJson(out obj))
                        target.Data[p.Name] = obj;
                    else
                        target.Data[p.Name] = value;
                } else if (jsonType == JsonType.Array) {
                    JArray obj;
                    if (value.TryFromJson(out obj))
                        target.Data[p.Name] = obj;
                    else
                        target.Data[p.Name] = value;
                } else
                    target.Data[p.Name] = value;
            }
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