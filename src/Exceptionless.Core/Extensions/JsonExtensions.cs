using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exceptionless.Core.Reflection;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Serializer;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Exceptionless.Core.Extensions {
    [System.Runtime.InteropServices.GuidAttribute("4186FC77-AF28-4D51-AAC3-49055DD855A4")]
    public static class JsonExtensions {
        public static bool IsNullOrEmpty(this JToken target) {
            if (target == null || target.Type == JTokenType.Null)
                return true;

            if (target.Type == JTokenType.Object || target.Type == JTokenType.Array)
                return !target.HasValues;

            if (target.Type != JTokenType.Property)
                return false;

            var value = ((JProperty)target).Value;
            if (value.Type == JTokenType.String)
                return value.ToString().IsNullOrEmpty();

            return IsNullOrEmpty(value);
        }

        public static bool IsPropertyNullOrEmpty(this JObject target, string name) {
            if (target[name] == null)
                return true;

            return target.Property(name).Value.IsNullOrEmpty();
        }

        public static bool RemoveIfNullOrEmpty(this JObject target, string name) {
            if (!target.IsPropertyNullOrEmpty(name))
                return false;

            target.Remove(name);
            return true;
        }

        public static void RemoveAll(this JObject target, params string[] names) {
            foreach (string name in names)
                target.Remove(name);
        }


        public static bool RemoveAllIfNullOrEmpty(this JObject target, params string[] names) {
            if (target.IsNullOrEmpty())
                return false;

            var properties = target.Descendants().OfType<JProperty>().Where(t => names.Contains(t.Name) && t.IsNullOrEmpty()).ToList();
            foreach(var p in properties)
                p.Remove();

            return true;
        }

        public static bool Rename(this JObject target, string currentName, string newName) {
            if (String.Equals(currentName, newName))
                return true;

            if (target[currentName] == null)
                return false;

            var p = target.Property(currentName);
            p.Replace(new JProperty(newName, p.Value));

            return true;
        }

        public static bool RenameOrRemoveIfNullOrEmpty(this JObject target, string currentName, string newName) {
            if (target[currentName] == null)
                return false;

            bool isNullOrEmpty = target.IsPropertyNullOrEmpty(currentName);
            var p = target.Property(currentName);
            if (isNullOrEmpty) {
                target.Remove(p.Name);
                return false;
            }

            p.Replace(new JProperty(newName, p.Value));
            return true;
        }

        public static void MoveOrRemoveIfNullOrEmpty(this JObject target, JObject source, params string[] names) {
            foreach (string name in names) {
                if (source[name] == null)
                    continue;

                bool isNullOrEmpty = source.IsPropertyNullOrEmpty(name);
                var p = source.Property(name);
                source.Remove(p.Name);

                if (isNullOrEmpty)
                    continue;

                target.Add(name, p.Value);
            }
        }

        public static bool RenameAll(this JObject target, string currentName, string newName) {
            var properties = target.Descendants().OfType<JProperty>().Where(t => t.Name == currentName).ToList();
            foreach (var p in properties) {
                if (p.Parent is JObject parent)
                    parent.Rename(currentName, newName);
            }

            return true;
        }

        public static string GetPropertyStringValue(this JObject target, string name) {
            if (target.IsPropertyNullOrEmpty(name))
                return null;

            return target.Property(name).Value.ToString();
        }


        public static string GetPropertyStringValueAndRemove(this JObject target, string name) {
            string value = target.GetPropertyStringValue(name);
            target.Remove(name);
            return value;
        }

        public static bool IsJson(this string value) {
            return value.GetJsonType() != JsonType.None;
        }

        public static JsonType GetJsonType(this string value) {
            if (String.IsNullOrEmpty(value))
                return JsonType.None;

            for (int i = 0; i < value.Length; i++) {
                if (Char.IsWhiteSpace(value[i]))
                    continue;

                if (value[i] == '{')
                    return JsonType.Object;

                if (value[i] == '[')
                    return JsonType.Array;

                break;
            }

            return JsonType.None;
        }

        public static string ToJson<T>(this T data, Formatting formatting = Formatting.None, JsonSerializerSettings settings = null) {
            var serializer = settings == null ? JsonSerializer.CreateDefault() : JsonSerializer.CreateDefault(settings);
            serializer.Formatting = formatting;

            using (var sw = new StringWriter()) {
                serializer.Serialize(sw, data, typeof(T));
                return sw.ToString();
            }
        }

        public static List<T> FromJson<T>(this JArray data, JsonSerializerSettings settings = null) {
            var serializer = settings == null ? JsonSerializer.CreateDefault() : JsonSerializer.CreateDefault(settings);
            return data.ToObject<List<T>>(serializer);
        }

        public static T FromJson<T>(this string data, JsonSerializerSettings settings = null) {
            var serializer = settings == null ? JsonSerializer.CreateDefault() : JsonSerializer.CreateDefault(settings);

            using (var sw = new StringReader(data))
            using (var sr = new JsonTextReader(sw))
                return serializer.Deserialize<T>(sr);
        }

        public static bool TryFromJson<T>(this string data, out T value, JsonSerializerSettings settings = null) {
            try {
                value = data.FromJson<T>(settings);
                return true;
            } catch (Exception) {
                value = default;
                return false;
            }
        }

        private static readonly ConcurrentDictionary<Type, IMemberAccessor> _countAccessors = new ConcurrentDictionary<Type, IMemberAccessor>();
        public static bool IsValueEmptyCollection(this JsonProperty property, object target) {
            object value = property.ValueProvider.GetValue(target);
            if (value == null)
                return true;

            if (value is ICollection collection)
                return collection.Count == 0;

            if (!_countAccessors.ContainsKey(property.PropertyType)) {
                if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType)) {
                    var countProperty = property.PropertyType.GetProperty("Count");
                    if (countProperty != null)
                        _countAccessors.AddOrUpdate(property.PropertyType, LateBinder.GetPropertyAccessor(countProperty));
                    else
                        _countAccessors.AddOrUpdate(property.PropertyType, null);
                } else {
                    _countAccessors.AddOrUpdate(property.PropertyType, null);
                }
            }

            var countAccessor = _countAccessors[property.PropertyType];
            if (countAccessor == null)
                return false;

            int count = (int)countAccessor.GetValue(value);
            return count == 0;
        }

        public static void AddModelConverters(this JsonSerializerSettings settings, ILogger logger) {
            var knownEventDataTypes = new Dictionary<string, Type> {
                { Event.KnownDataKeys.Error, typeof(Error) },
                { Event.KnownDataKeys.EnvironmentInfo, typeof(EnvironmentInfo) },
                { Event.KnownDataKeys.Location, typeof(Location) },
                { Event.KnownDataKeys.RequestInfo, typeof(RequestInfo) },
                { Event.KnownDataKeys.SimpleError, typeof(SimpleError) },
                { Event.KnownDataKeys.SubmissionClient, typeof(SubmissionClient) },
                { Event.KnownDataKeys.ManualStackingInfo, typeof(ManualStackingInfo) },
                { Event.KnownDataKeys.UserDescription, typeof(UserDescription) },
                { Event.KnownDataKeys.UserInfo, typeof(UserInfo) }
            };

            var knownProjectDataTypes = new Dictionary<string, Type> {
                { Project.KnownDataKeys.SlackToken, typeof(SlackToken) }
            };

            settings.Converters.Add(new DataObjectConverter<Organization>(logger));
            settings.Converters.Add(new DataObjectConverter<Project>(logger, knownProjectDataTypes));
            settings.Converters.Add(new DataObjectConverter<PersistentEvent>(logger, knownEventDataTypes));
            settings.Converters.Add(new DataObjectConverter<Event>(logger, knownEventDataTypes));
            settings.Converters.Add(new DataObjectConverter<EnvironmentInfo>(logger));
            settings.Converters.Add(new DataObjectConverter<Error>(logger));
            settings.Converters.Add(new DataObjectConverter<InnerError>(logger));
            settings.Converters.Add(new DataObjectConverter<Method>(logger));
            settings.Converters.Add(new DataObjectConverter<Module>(logger));
            settings.Converters.Add(new DataObjectConverter<Parameter>(logger));
            settings.Converters.Add(new DataObjectConverter<RequestInfo>(logger));
            settings.Converters.Add(new DataObjectConverter<SimpleError>(logger));
            settings.Converters.Add(new DataObjectConverter<StackFrame>(logger));
            settings.Converters.Add(new DataObjectConverter<UserDescription>(logger));
            settings.Converters.Add(new DataObjectConverter<UserInfo>(logger));
        }
    }

    public enum JsonType : byte {
        None,
        Object,
        Array
    }
}