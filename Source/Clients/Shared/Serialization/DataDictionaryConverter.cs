#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Json;
using Exceptionless.Json.Converters;
using Exceptionless.Json.Linq;
using Exceptionless.Models;

namespace Exceptionless.Serialization {
    internal class DataDictionaryConverter : CustomCreationConverter<DataDictionary> {
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
}