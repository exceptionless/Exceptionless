using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
#if EMBEDDED
using Exceptionless.Json;
using Exceptionless.Json.Serialization;
#else
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
#endif

namespace Exceptionless.Serializer {
#if EMBEDDED
    internal
#else
    public
#endif
    class ExtensionContractResolver : DefaultContractResolver {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
            JsonProperty property = base.CreateProperty(member, memberSerialization);
            Predicate<object> shouldSerialize = property.ShouldSerialize;
            property.ShouldSerialize = obj => !IsDataProperty(property) && (shouldSerialize == null || shouldSerialize(obj));
            return property;
        }

        private bool IsDataProperty(JsonProperty property) {
            return (typeof(IData).IsAssignableFrom(property.PropertyType.DeclaringType)
                && property.PropertyType == typeof(DataDictionary)
                && property.PropertyType.Name == "Data");
        }

        protected override JsonObjectContract CreateObjectContract(Type objectType) {
            var contract = base.CreateObjectContract(objectType);
            if (!typeof(IData).IsAssignableFrom(objectType))
                return contract;

            contract.ExtensionDataGetter = o => {
                var dataObject = o as IData;
                if (dataObject == null)
                    return null;
                return dataObject.Data.Select(kvp => new KeyValuePair<object, object>(kvp.Key, kvp.Value));
            };
            contract.ExtensionDataSetter = (o, key, value) => {
                var dataObject = o as IData;
                if (dataObject == null)
                    return;

                if (dataObject.Data == null)
                    dataObject.Data = new DataDictionary();

                dataObject.Data.Add(key, value.ToJson());
            };

            return contract;
        }
    }
}