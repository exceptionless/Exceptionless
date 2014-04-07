using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Exceptionless.Extensions;
using Exceptionless.Json;
using Exceptionless.Json.Converters;
using Exceptionless.Json.Linq;
using Exceptionless.Json.Serialization;
using Exceptionless.Models;
using Exceptionless.Models.Data;

namespace Exceptionless.Serializer {
    public class DefaultJsonSerializer : IJsonSerializer {
        internal static IJsonSerializer Instance = new DefaultJsonSerializer();

        protected readonly JsonSerializerSettings _serializerSettings;

        public DefaultJsonSerializer() {
            _serializerSettings = new JsonSerializerSettings {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                PreserveReferencesHandling = PreserveReferencesHandling.None
            };

            _serializerSettings.Converters.Add(new StringEnumConverter());
            _serializerSettings.Converters.Add(new DataDictionaryConverter());
            _serializerSettings.Converters.Add(new RequestInfoConverter());
        }

        public virtual string Serialize(object model, string[] exclusions = null, int maxDepth = 5, bool continueOnSerializationError = true) {
            if (model == null)
                return null;

            JsonSerializer serializer = JsonSerializer.Create(_serializerSettings);
            if (maxDepth < 1)
                maxDepth = Int32.MaxValue;

            using (var sw = new StringWriter()) {
                using (var jw = new JsonTextWriterWithDepth(sw)) {
                    jw.Formatting = Formatting.None;
                    Func<JsonProperty, bool> include = p => ShouldSerialize(jw, p, maxDepth, exclusions);
                    var resolver = new ConditionalContractResolver(include);
                    serializer.ContractResolver = resolver;
                    if (continueOnSerializationError)
                        serializer.Error += (sender, args) => { args.ErrorContext.Handled = true; };

                    serializer.Serialize(jw, model);
                }

                return sw.ToString();
            }
        }

        public virtual object Deserialize(string json, Type type) {
            if (String.IsNullOrWhiteSpace(json))
                return null;

            return JsonConvert.DeserializeObject(json, type, _serializerSettings);
        }

        private bool ShouldSerialize(JsonTextWriterWithDepth jw, JsonProperty property, int maxDepth, IEnumerable<string> excludedPropertyNames) {
            if (excludedPropertyNames != null && property.PropertyName.AnyWildcardMatches(excludedPropertyNames, true))
                return false;

            bool serializesAsObject = !IsIntrinsicType(property.PropertyType);
            return serializesAsObject ? jw.CurrentDepth < maxDepth : jw.CurrentDepth <= maxDepth;
        }

        private static bool IsIntrinsicType(Type t) {
            if (t == typeof(string))
                return true;

            if (!t.IsValueType)
                return false;

            if (t == typeof(bool))
                return true;
            if (t == typeof(DateTime))
                return true;
            if (t == typeof(DateTimeOffset))
                return true;
            if (t == typeof(Int16))
                return true;
            if (t == typeof(Int32))
                return true;
            if (t == typeof(Int64))
                return true;
            if (t == typeof(UInt16))
                return true;
            if (t == typeof(UInt32))
                return true;
            if (t == typeof(UInt64))
                return true;
            if (t == typeof(float))
                return true;
            if (t == typeof(double))
                return true;
            if (t == typeof(char))
                return true;
            if (t == typeof(byte))
                return true;
            if (t == typeof(byte[]))
                return true;
            if (t == typeof(sbyte))
                return true;
            if (t == typeof(decimal))
                return true;
            if (t == typeof(Guid))
                return true;
            if (t == typeof(TimeSpan))
                return true;
            if (t == typeof(Uri))
                return true;

            return false;
        }

        private class JsonTextWriterWithDepth : JsonTextWriter {
            public JsonTextWriterWithDepth(TextWriter textWriter) : base(textWriter) {}

            public int CurrentDepth { get; private set; }

            public override void WriteStartObject() {
                CurrentDepth++;
                base.WriteStartObject();
            }

            public override void WriteEndObject() {
                CurrentDepth--;
                base.WriteEndObject();
            }
        }

        private class ConditionalContractResolver : DefaultContractResolver {
            private readonly Func<JsonProperty, bool> _includeProperty;

            public ConditionalContractResolver(Func<JsonProperty, bool> includeProperty) {
                _includeProperty = includeProperty;
            }

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
                JsonProperty property = base.CreateProperty(member, memberSerialization);
                Predicate<object> shouldSerialize = property.ShouldSerialize;
                property.ShouldSerialize = obj => _includeProperty(property) && (shouldSerialize == null || shouldSerialize(obj));
                return property;
            }
        }

        private class DataDictionaryConverter : CustomCreationConverter<DataDictionary> {
            public override DataDictionary Create(Type objectType) {
                return new DataDictionary();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
                object obj = base.ReadJson(reader, objectType, existingValue, serializer);
                var result = obj as DataDictionary;
                if (result == null)
                    return obj;

                var dictionary = new DataDictionary();
                foreach (string key in result.Keys) {
                    object value = result[key];
                    if (value is JObject)
                        dictionary[key] = value.ToString();
                    else if (value is JArray)
                        dictionary[key] = value.ToString();
                    else
                        dictionary[key] = value;
                }

                return dictionary;
            }
        }

        private class RequestInfoConverter : CustomCreationConverter<RequestInfo> {
            public override RequestInfo Create(Type objectType) {
                return new RequestInfo();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
                object obj = base.ReadJson(reader, objectType, existingValue, serializer);
                var result = obj as RequestInfo;
                if (result == null)
                    return obj;

                if (result.PostData is JObject)
                    result.PostData = result.PostData.ToString();

                return result;
            }
        }
    }
}