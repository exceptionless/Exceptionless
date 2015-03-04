using System;
using System.Reflection;
using Exceptionless.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Serializer {
    public class ExceptionlessContractResolver : DefaultContractResolver {
        private readonly Func<JsonProperty, object, bool> _includeProperty;

        public ExceptionlessContractResolver(Func<JsonProperty, object, bool> includeProperty = null) {
            _includeProperty = includeProperty;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
            JsonProperty property = base.CreateProperty(member, memberSerialization);
            if (_includeProperty == null)
                return property;
            
            Predicate<object> shouldSerialize = property.ShouldSerialize;
            property.ShouldSerialize = obj => _includeProperty(property, obj) && (shouldSerialize == null || shouldSerialize(obj));
            return property;
        }

        protected override JsonDictionaryContract CreateDictionaryContract(Type objectType) {
            if (objectType != typeof(DataDictionary) && objectType != typeof(SettingsDictionary))
                return base.CreateDictionaryContract(objectType);

            JsonDictionaryContract contract = base.CreateDictionaryContract(objectType);
            contract.PropertyNameResolver = propertyName => propertyName;
            return contract;
        }

        protected override string ResolvePropertyName(string propertyName) {
            return propertyName.ToLowerUnderscoredWords();
        }
    }
}