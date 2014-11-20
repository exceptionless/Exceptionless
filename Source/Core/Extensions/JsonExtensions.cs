#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeSmith.Core.Extensions;
using CodeSmith.Core.Reflection;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Exceptionless.Serializer;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
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

        public static bool RemoveAllIfNullOrEmpty(this IEnumerable<JProperty> elements, params string[] names) {
            if (elements == null)
                return false;

            foreach (var p in elements.Where(t => names.Contains(t.Name) && t.IsNullOrEmpty()))
                p.Remove();

            return true;
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
            if (target[currentName] == null)
                return false;

            JProperty p = target.Property(currentName);
            target.Remove(p.Name);
            target.Add(newName, p.Value);

            return true;
        }

        public static bool RenameOrRemoveIfNullOrEmpty(this JObject target, string currentName, string newName) {
            if (target[currentName] == null)
                return false;

            bool isNullOrEmpty = target.IsPropertyNullOrEmpty(currentName);
            JProperty p = target.Property(currentName);
            target.Remove(p.Name);

            if (isNullOrEmpty)
                return false;

            target.Add(newName, p.Value);
            return true;
        }

        public static void MoveOrRemoveIfNullOrEmpty(this JObject target, JObject source, params string[] names) {
            foreach (var name in names) {
                if (source[name] == null)
                    continue;

                bool isNullOrEmpty = source.IsPropertyNullOrEmpty(name);
                JProperty p = source.Property(name);
                source.Remove(p.Name);

                if (isNullOrEmpty)
                    continue;

                target.Add(name, p.Value);
            }
        }

        public static bool RenameAll(this IEnumerable<JProperty> properties, string currentName, string newName) {
            foreach (var p in properties.Where(t => t.Name == currentName)) {
                var parent = p.Parent as JObject;
                if (parent != null)
                    parent.Rename(currentName, newName);
            }

            return true;
        }

        public static bool RenameAll(this JObject target, string currentName, string newName) {
            var properties = target.Descendants().OfType<JProperty>().Where(t => t.Name == currentName).ToList();
            foreach (var p in properties) {
                var parent = p.Parent as JObject;
                if (parent != null)
                    parent.Rename(currentName, newName);
            }

            return true;
        }
        
        public static bool CopyOrRemoveIfNullOrEmpty(this JObject target, JObject source, string name) {
            if (source[name] == null)
                return false;

            bool isNullOrEmpty = source.IsPropertyNullOrEmpty(name);
            if (isNullOrEmpty)
                return false;

            target.Add(name, source.Property(name).Value);
            return true;
        }

        public static string GetPropertyStringValue(this JObject target, string name) {
            if (target.IsPropertyNullOrEmpty(name)) 
                return null;

            return target.Property(name).Value.ToString();
        }


        public static string GetPropertyStringValueAndRemove(this JObject target, string name) {
            var value = target.GetPropertyStringValue(name);
            target.Remove(name);
            return value;
        }

        public static string ToJson<T>(this T data, Formatting formatting = Formatting.None, JsonSerializerSettings settings = null) {
            JsonSerializer serializer = settings == null ? JsonSerializer.CreateDefault() : JsonSerializer.CreateDefault(settings);
            serializer.Formatting = formatting;

            using (var sw = new StringWriter()) {
                serializer.Serialize(sw, data, typeof(T));
                return sw.ToString();
            }
        }

        public static List<T> FromJson<T>(this JArray data, JsonSerializerSettings settings = null) {
            JsonSerializer serializer = settings == null ? JsonSerializer.CreateDefault() : JsonSerializer.CreateDefault(settings);
            return data.ToObject<List<T>>(serializer);
        }

        public static T FromJson<T>(this JObject data, JsonSerializerSettings settings = null) {
            JsonSerializer serializer = settings == null ? JsonSerializer.CreateDefault() : JsonSerializer.CreateDefault(settings);
            return data.ToObject<T>(serializer);
        }

        public static object FromJson(this string data, Type objectType, JsonSerializerSettings settings = null) {
            JsonSerializer serializer = settings == null ? JsonSerializer.CreateDefault() : JsonSerializer.CreateDefault(settings);

            using (var sw = new StringReader(data))
            using (var sr = new JsonTextReader(sw))
                return serializer.Deserialize(sr, objectType);
        }

        public static T FromJson<T>(this string data, JsonSerializerSettings settings = null) {
            JsonSerializer serializer = settings == null ? JsonSerializer.CreateDefault() : JsonSerializer.CreateDefault(settings);

            using (var sw = new StringReader(data))
            using (var sr = new JsonTextReader(sw))
                return serializer.Deserialize<T>(sr);
        }

        public static bool TryFromJson<T>(this string data, out T value, JsonSerializerSettings settings = null) {
            try {
                value = data.FromJson<T>(settings);
                return true;
            } catch (Exception ex) {
                value = default(T);
                return false;
            }
        }

        public static byte[] ToBson<T>(this T data, JsonSerializerSettings settings = null) {
            JsonSerializer serializer = settings == null ? JsonSerializer.CreateDefault() : JsonSerializer.CreateDefault(settings);
            using (var ms = new MemoryStream()) {
                using (var writer = new BsonWriter(ms))
                    serializer.Serialize(writer, data, typeof(T));

                return ms.ToArray();
            }
        }

        public static T FromBson<T>(this byte[] data, JsonSerializerSettings settings = null) {
            JsonSerializer serializer = settings == null ? JsonSerializer.CreateDefault() : JsonSerializer.CreateDefault(settings);

            using (var sw = new MemoryStream(data))
                using (var sr = new BsonReader(sw))
                    return serializer.Deserialize<T>(sr);
        
        }

        public static object FromBson(this byte[] data, Type objectType, JsonSerializerSettings settings = null) {
            JsonSerializer serializer = settings == null ? JsonSerializer.CreateDefault() : JsonSerializer.CreateDefault(settings);

            using (var sw = new MemoryStream(data))
            using (var sr = new BsonReader(sw))
                return serializer.Deserialize(sr, objectType);

        }

        public static bool TryFromBson<T>(this byte[] data, out T value, JsonSerializerSettings settings = null) {
            try {
                value = data.FromBson<T>(settings);
                return true;
            } catch (Exception ex) {
                value = default(T);
                return false;
            }
        }

        public static bool TryFromBson(this byte[] data, out object value, Type objectType, JsonSerializerSettings settings = null) {
            try {
                value = data.FromBson(objectType, settings);
                return true;
            } catch (Exception ex) {
                value = null;
                return false;
            }
        }

        private static readonly Dictionary<Type, IMemberAccessor> _countAccessors = new Dictionary<Type, IMemberAccessor>();
        public static bool IsValueEmptyCollection(this JsonProperty property, object target) {
            var value = property.ValueProvider.GetValue(target);
            if (value == null)
                return true;

            var collection = value as ICollection;
            if (collection != null && collection.Count == 0)
                return true;

            if (!_countAccessors.ContainsKey(property.PropertyType)) {
                if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType)) {
                    var countProperty = property.PropertyType.GetProperty("Count");
                    if (countProperty != null)
                        _countAccessors[property.PropertyType] = LateBinder.GetPropertyAccessor(countProperty);
                    else
                        _countAccessors[property.PropertyType] = null;
                } else {
                    _countAccessors[property.PropertyType] = null;
                }
            }

            var countAccessor = _countAccessors[property.PropertyType];
            if (countAccessor == null)
                return false;

            var count = (int)countAccessor.GetValue(value);
            return count == 0;
        }

        public static void AddModelConverters(this JsonSerializerSettings settings) {
            var knownDataTypes = new Dictionary<string, Type> {
                { Event.KnownDataKeys.EnvironmentInfo, typeof(EnvironmentInfo) },
                { Event.KnownDataKeys.RequestInfo, typeof(RequestInfo) },
                { Event.KnownDataKeys.SimpleError, typeof(SimpleError) },
                { Event.KnownDataKeys.UserDescription, typeof(UserDescription) },
                { Event.KnownDataKeys.UserInfo, typeof(UserInfo) },
                { Event.KnownDataKeys.Error, typeof(Error) }
            };

            settings.Converters.Add(new DataObjectConverter<PersistentEvent>(knownDataTypes));
            settings.Converters.Add(new DataObjectConverter<Event>(knownDataTypes));
            settings.Converters.Add(new DataObjectConverter<EnvironmentInfo>());
            settings.Converters.Add(new DataObjectConverter<Error>());
            settings.Converters.Add(new DataObjectConverter<InnerError>());
            settings.Converters.Add(new DataObjectConverter<Method>());
            settings.Converters.Add(new DataObjectConverter<Module>());
            settings.Converters.Add(new DataObjectConverter<Parameter>());
            settings.Converters.Add(new DataObjectConverter<RequestInfo>());
            settings.Converters.Add(new DataObjectConverter<SimpleError>());
            settings.Converters.Add(new DataObjectConverter<StackFrame>());
            settings.Converters.Add(new DataObjectConverter<UserDescription>());
            settings.Converters.Add(new DataObjectConverter<UserInfo>());
        }
    }
}