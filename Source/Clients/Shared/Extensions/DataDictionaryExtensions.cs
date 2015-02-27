#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Exceptionless.Dependency;
using Exceptionless.Extensions;
using Exceptionless.Models;

namespace Exceptionless {
    public static class DataDictionaryExtensions {
        public static T GetValue<T>(this DataDictionary items, string key, IJsonSerializer serializer = null) {
            if (items == null)
                throw new ArgumentNullException("items");

            if (String.IsNullOrEmpty(key))
                throw new ArgumentNullException("key");

            object data;
            if (!items.TryGetValue(key, out data))
                throw new KeyNotFoundException(String.Format("The key '{0}' was not found.", key));

            if (data == null || data is T)
                return (T)data;

            var json = data as string ?? data.ToString();
            serializer = serializer ?? DependencyResolver.Default.GetJsonSerializer();
            return serializer.Deserialize<T>(json);
        }

        /// <summary>
        /// Sets a property value on the extended data.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <param name="name">The name of the property to add.</param>
        /// <param name="value">The property value to add.</param>
        /// <param name="maxDepth">The max depth of the object to include. Used when the property value is an object.</param>
        /// <param name="excludedPropertyNames">Any property names that should be excluded in complex object values.</param>
        /// <param name="ignoreSerializationErrors">Specifies wether complex object properties that throw errors while serializing be ignored</param>
        /// <param name="client">
        /// The ExceptionlessClient instance used for configuration. If a client is not specified, it will use
        /// ExceptionlessClient.Default.
        /// </param>
        public static void SetProperty(this IData target, string name, object value, int? maxDepth = null, IEnumerable<string> excludedPropertyNames = null, bool ignoreSerializationErrors = false, ExceptionlessClient client = null) {
            AddObject(target, value, name, maxDepth, excludedPropertyNames, ignoreSerializationErrors, client);
        }

        /// <summary>
        /// Adds the object to extended data.
        /// </summary>
        /// <param name="data">The error.</param>
        /// <param name="value">The data object to add.</param>
        /// <param name="name">The name of the object to add. If not specified, the name will be implied from the object type.</param>
        /// <param name="maxDepth">The max depth of the object to include.</param>
        /// <param name="excludedPropertyNames">Any property names that should be excluded</param>
        /// <param name="ignoreSerializationErrors">Specifies wether properties that throw errors while serializing be ignored</param>
        /// <param name="client">
        /// The ExceptionlessClient instance used for configuration. If a client is not specified, it will use
        /// ExceptionlessClient.Default.
        /// </param>
        public static void AddObject(this IData data, object value, string name = null, int? maxDepth = null, IEnumerable<string> excludedPropertyNames = null, bool ignoreSerializationErrors = false, ExceptionlessClient client = null) {
            if (client == null)
                client = ExceptionlessClient.Default;

            if (value == null)
                return;

            ExtendedDataInfo info;
            if (value is ExtendedDataInfo)
                info = value as ExtendedDataInfo;
            else {
                info = new ExtendedDataInfo {
                    Data = value,
                    Name = name,
                    MaxDepthToSerialize = maxDepth,
                    ExcludedPropertyNames = excludedPropertyNames != null ? client.Configuration.DataExclusions.Union(excludedPropertyNames).ToArray() : client.Configuration.DataExclusions.ToArray(),
                    IgnoreSerializationErrors = ignoreSerializationErrors
                };
            }

            AddObject(data, info);
        }

        /// <summary>
        /// Adds the object to extended data.
        /// </summary>
        /// <param name="data">The error to add the object to.</param>
        /// <param name="info">The data object to add.</param>
        /// <param name="client">
        /// The ExceptionlessClient instance used for configuration. If a client is not specified, it will use
        /// ExceptionlessClient.Default.
        /// </param>
        public static void AddObject(this IData data, ExtendedDataInfo info, ExceptionlessClient client = null) {
            if (client == null)
                client = ExceptionlessClient.Default;

            if (info == null || info.Data == null)
                return;

            string name = !String.IsNullOrWhiteSpace(info.Name) ? info.Name.Trim() : null;
            if (String.IsNullOrEmpty(name)) {
                name = info.Data.GetType().Name;
                int index = 1;
                while (data.Data.ContainsKey(name))
                    name = info.Data.GetType().Name + index++;
            }

            Type dataType = info.Data.GetType();
            if (dataType == typeof(bool) || dataType == typeof(string) || dataType.IsNumeric()) {
                if (data.Data.ContainsKey(name))
                    data.Data[name] = info.Data;
                else
                    data.Data.Add(name, info.Data);

                return;
            }

            string json;
            try {
                if (dataType.IsPrimitiveType()) {
                    json = info.Data.ToString();
                } else {
                    string[] excludedPropertyNames = info.ExcludedPropertyNames != null ? client.Configuration.DataExclusions.Union(info.ExcludedPropertyNames).ToArray() : client.Configuration.DataExclusions.ToArray();

                    var serializer = DependencyResolver.Default.GetJsonSerializer();
                    json = serializer.Serialize(info.Data, excludedPropertyNames, info.MaxDepthToSerialize.HasValue ? info.MaxDepthToSerialize.Value : 5, info.IgnoreSerializationErrors);
                }
            } catch (Exception ex) {
                json = ex.ToString();
            }

            if (String.IsNullOrEmpty(json))
                return;

            if (data.Data.ContainsKey(name))
                data.Data[name] = json;
            else
                data.Data.Add(name, json);
        }
    }
}