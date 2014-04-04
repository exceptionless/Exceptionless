#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Exceptionless.Extensions;
using Exceptionless.Json;
using Exceptionless.Json.Converters;
using Exceptionless.Json.Serialization;

namespace Exceptionless.Serialization {
    /// <summary>
    /// The model serializer.
    /// </summary>
    public class ModelSerializer {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModelSerializer" /> class.
        /// </summary>
        public ModelSerializer() {
            DefaultSettings = GetSettings();
        }

        private static readonly Lazy<ModelSerializer> _serializer = new Lazy<ModelSerializer>(() => new ModelSerializer());

        private static JsonSerializerSettings GetSettings() {
            var settings = new JsonSerializerSettings {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Include,
                PreserveReferencesHandling = PreserveReferencesHandling.None
            };

            settings.Converters.Add(new StringEnumConverter());
            settings.Converters.Add(new DataDictionaryConverter());
            settings.Converters.Add(new RequestInfoConverter());

            return settings;
        }

        public static ModelSerializer Current { get { return _serializer.Value; } }

        /// <summary>
        /// Gets or sets the serializer default settings.
        /// </summary>
        /// <value>The json serializer default settings.</value>
        public JsonSerializerSettings DefaultSettings { get; private set; }

        /// <summary>
        /// Serialize the specified model to a stream.
        /// </summary>
        /// <param name="stream">The stream to write the serialization to.</param>
        /// <param name="model">The model to serialize.</param>
        public void Serialize(Stream stream, object model) {
            using (var sw = new StreamWriter(stream, Encoding.UTF8))
                Serialize(sw, model);
        }

        /// <summary>
        /// Serialize the specified model to a TextWriter.
        /// </summary>
        /// <param name="writer">The TextWriter to write the serialization to.</param>
        /// <param name="model">The model to serialize.</param>
        public void Serialize(TextWriter writer, object model) {
            JsonSerializer serializer = JsonSerializer.Create(DefaultSettings);

            using (var jw = new JsonTextWriter(writer) {
                Formatting = Formatting.Indented
            }) {
                serializer.Serialize(jw, model);
                jw.Flush();
            }
        }

        /// <summary>
        /// Deserialize the specified model from the stream.
        /// </summary>
        /// <param name="path">The path to the file to deserialize.</param>
        /// <returns>An instance of the the serialized model.</returns>
        public T Deserialize<T>(string path) {
            using (StreamReader jr = File.OpenText(path))
                return Deserialize<T>(jr);
        }

        /// <summary>
        /// Deserialize the specified model from the stream.
        /// </summary>
        /// <param name="stream">The stream to read the serialized data from.</param>
        /// <returns>An instance of the the serialized model.</returns>
        public T Deserialize<T>(Stream stream) {
            using (var sr = new StreamReader(stream))
                return Deserialize<T>(sr);
        }

        /// <summary>
        /// Deserialize the specified model from the stream.
        /// </summary>
        /// <param name="stream">The stream to read the serialized data from.</param>
        /// <param name="type">Type of the model.</param>
        /// <returns>An instance of the the serialized model.</returns>
        public object Deserialize(Stream stream, Type type = null) {
            using (var sr = new StreamReader(stream))
                return Deserialize(sr, type);
        }

        /// <summary>
        /// Deserialize the specified model from the TextReader.
        /// </summary>
        /// <param name="reader">The TextReader to read the serialized data from.</param>
        /// <returns>An instance of the the serialized model.</returns>
        public T Deserialize<T>(TextReader reader) {
            JsonSerializer serializer = JsonSerializer.Create(DefaultSettings);
            using (var jr = new JsonTextReader(reader))
                return (T)serializer.Deserialize(jr, typeof(T));
        }

        /// <summary>
        /// Deserialize the specified model from the TextReader.
        /// </summary>
        /// <param name="reader">The TextReader to read the serialized data from.</param>
        /// <param name="type">Type of the model.</param>
        /// <returns>An instance of the the serialized model.</returns>
        public object Deserialize(TextReader reader, Type type = null) {
            JsonSerializer serializer = JsonSerializer.Create(DefaultSettings);
            using (var jr = new JsonTextReader(reader))
                return serializer.Deserialize(jr, type);
        }

        public void SerializeToFile(string path, object data) {
            JsonSerializer serializer = JsonSerializer.Create(DefaultSettings);
            using (JsonWriter jw = new JsonTextWriter(File.CreateText(path))) {
                jw.Formatting = Formatting.Indented;
                serializer.Serialize(jw, data);
            }
        }

        public string SerializeToString(object data, int? maxDepth = null, ICollection<string> excludedPropertyNames = null, bool ignoreSerializationErrors = false) {
            JsonSerializer serializer = JsonSerializer.Create(DefaultSettings);
            if (!maxDepth.HasValue)
                maxDepth = Int32.MaxValue;

            using (var sw = new StringWriter()) {
                using (var jw = new JsonTextWriterWithDepth(sw)) {
                    jw.Formatting = Formatting.Indented;
                    Func<JsonProperty, bool> include = p => ShouldSerialize(jw, p, maxDepth.Value, excludedPropertyNames);
                    var resolver = new ConditionalContractResolver(include);
                    serializer.ContractResolver = resolver;
                    if (ignoreSerializationErrors)
                        serializer.Error += (sender, args) => { args.ErrorContext.Handled = true; };
                    serializer.Serialize(jw, data);
                }

                return sw.ToString();
            }
        }

        private bool ShouldSerialize(JsonTextWriterWithDepth jw, JsonProperty property, int maxDepth, ICollection<string> excludedPropertyNames) {
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
    }
}