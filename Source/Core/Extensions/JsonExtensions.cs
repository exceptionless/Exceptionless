#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Extensions {
    [System.Runtime.InteropServices.GuidAttribute("4186FC77-AF28-4D51-AAC3-49055DD855A4")]
    public static class JsonExtensions {
        public static bool IsNullOrEmpty(this JToken target) {
            return target == null 
                || target.Type == JTokenType.Null 
                || (target.Type == JTokenType.Array && !target.HasValues)
                || (target.Type == JTokenType.Object && !target.HasValues);
        }

        public static bool IsNullOrEmpty(this JObject target, string name) {
            if (target[name] == null)
                return true;

            return target.Property(name).Value.IsNullOrEmpty();
        }

        public static bool Rename(this JObject target, string currentName, string newName) {
            if (target[currentName] == null)
                return false;

            JProperty p = target.Property(currentName);
            target.Remove(p.Name);
            target.Add(newName, p.Value);

            return true;
        }

        public static bool RemoveIfNullOrEmpty(this JObject target, string name) {
            if (!target.IsNullOrEmpty(name))
                return false;

            target.Remove(name);
            return true;
        }

        public static bool RenameOrRemoveIfNullOrEmpty(this JObject target, string currentName, string newName) {
            if (target[currentName] == null)
                return false;

            bool isNullOrEmpty = target.IsNullOrEmpty(currentName);
            JProperty p = target.Property(currentName);
            target.Remove(p.Name);

            if (isNullOrEmpty)
                return false;

            target.Add(newName, p.Value);
            return true;
        }

        public static bool RenameAll(this JObject target, string currentName, string newName) {
            var tokens = target.SelectTokens(currentName);
            if (!tokens.Any())
                return false;

            foreach (var token in tokens) {
               // token.Remove();
            }

            return true;
        }
        
        public static bool CopyOrRemoveIfNullOrEmpty(this JObject target, JObject source, string name) {
            if (source[name] == null)
                return false;

            bool isNullOrEmpty = source.IsNullOrEmpty(name);
            JProperty p = source.Property(name);
            source.Remove(p.Name);

            if (isNullOrEmpty)
                return false;

            target.Add(name, p.Value);
            return true;
        }

        public static string GetPropertyStringValue(this JObject target, string name) {
            if (target.IsNullOrEmpty(name)) 
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
    }
}