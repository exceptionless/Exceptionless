using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Exceptionless.Serializer {
    public class ExtensionContractResolver : DefaultContractResolver {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
            JsonProperty property = base.CreateProperty(member, memberSerialization);
            if (!typeof(IData).IsAssignableFrom(member.DeclaringType))
                return property;

            Predicate<object> shouldSerialize = property.ShouldSerialize;
            property.ShouldSerialize = obj => !IsDataProperty(property) && (shouldSerialize == null || shouldSerialize(obj)) && !IsEmptyCollection(property, obj);
            return property;
        }

        private bool IsDataProperty(JsonProperty property) {
            return (typeof(IData).IsAssignableFrom(property.DeclaringType)
                && property.PropertyType == typeof(DataDictionary)
                && property.PropertyName == "Data");
        }

        private bool IsEmptyCollection(JsonProperty property, object target) {
            var value = property.ValueProvider.GetValue(target);
            var collection = value as ICollection;
            if (collection != null && collection.Count == 0)
                return true;

            if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType)) {
                var countProp = property.PropertyType.GetProperty("Count");
                if (countProp == null)
                    return false;
                
                var count = (int)countProp.GetValue(value, null);
                return count == 0;
            }

            return false;
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

                if (IsPrimitiveType(value.GetType()))
                    dataObject.Data.Add(key, value.ToString());
                else
                    dataObject.Data.Add(key, value.ToJson());
            };

            return contract;
        }

        private bool IsPrimitiveType(Type type) {
            if (type.IsPrimitive)
                return true;

            if (type == typeof(Decimal)
                || type == typeof(DateTime)
                || type == typeof(String)
                || type == typeof(DateTimeOffset)
                || type == typeof(Guid)
                || type == typeof(TimeSpan)
                || type == typeof(Uri))
                return true;

            if (type.IsEnum)
                return true;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return IsPrimitiveType(Nullable.GetUnderlyingType(type));

            return false;
        }
    }

    class EmptyCollectionContractResolver : DefaultContractResolver {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            Predicate<object> shouldSerialize = property.ShouldSerialize;
            property.ShouldSerialize = obj => (shouldSerialize == null || shouldSerialize(obj)) && !property.IsValueEmptyCollection(obj);
            return property;
        }
    }
}