using System;
using System.Linq.Expressions;
using System.Reflection;
#if EMBEDDED
using Exceptionless.Json;
using Exceptionless.Json.Converters;
using Exceptionless.Json.Linq;
using Exceptionless.Extensions;
#else
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
#endif
using Exceptionless.Models;

namespace Exceptionless.Serializer {
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