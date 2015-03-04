using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NLog.Fluent;

namespace Exceptionless.Serializer {
    public class DataObjectConverter<T> : CustomCreationConverter<T> where T : IData, new() {
        private readonly IDictionary<string, Type> _dataTypeRegistry = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        private static IDictionary<string, IMemberAccessor> _propertyAccessors = new Dictionary<string, IMemberAccessor>(StringComparer.OrdinalIgnoreCase);
        private readonly char[] _filteredChars = { '.', '-', '_' };

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
                string propertyName = p.Name.ToLowerFiltered(_filteredChars);

                if (propertyName == "data" && p.Value is JObject) {
                    foreach (var dataProp in ((JObject)p.Value).Properties())
                        AddDataEntry(serializer, dataProp, target);

                    continue;
                }

                IMemberAccessor value;
                var accessor = _propertyAccessors.TryGetValue(propertyName, out value) ? value : null;
                if (accessor != null) {
                    if (p.Value.Type == JTokenType.None || p.Value.Type == JTokenType.Undefined)
                        continue;

                    if (p.Value.Type == JTokenType.Null) {
                        accessor.SetValue(target, null);
                        continue;
                    }

                    if (accessor.MemberType == typeof(DateTime)) {
                        accessor.SetValue(target, p.Value.ToObject<DateTimeOffset>(serializer).DateTime);
                        continue;
                    }

                    if (accessor.MemberType == typeof(DateTime?)) {
                        var offset = p.Value.ToObject<DateTimeOffset?>(serializer);
                        accessor.SetValue(target, offset.HasValue ? offset.Value.DateTime : (DateTime?)null);
                        continue;
                    }

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
            Type dataType;
            if (_dataTypeRegistry.TryGetValue(p.Name, out dataType)) {
                try {
                    string dataKey = p.Name;
                    if (target.Data.ContainsKey(dataKey))
                        dataKey = "_" + dataKey;
                    if (p.Value is JValue && p.Value.Type == JTokenType.String)
                        target.Data[dataKey] = serializer.Deserialize(new StringReader(p.Value.ToString()), dataType);
                    else
                        target.Data[dataKey] = p.Value.ToObject(dataType, serializer);

                    return;
                } catch (Exception) {
                    Log.Info().Message("Error deserializing known data type \"{0}\": {1}", p.Name, p.Value.ToString()).Write();
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