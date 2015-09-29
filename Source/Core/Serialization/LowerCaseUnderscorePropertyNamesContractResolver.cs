using System;
using System.Reflection;
using Exceptionless.Core.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Exceptionless.Core.Serialization {
    public class LowerCaseUnderscorePropertyNamesContractResolver : DefaultContractResolver {
        private readonly Func<Type, bool> _useDefaultContract;

        public LowerCaseUnderscorePropertyNamesContractResolver(Func<Type, bool> useDefaultContract = null) {
            _useDefaultContract = useDefaultContract;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            Predicate<object> shouldSerialize = property.ShouldSerialize;
            property.ShouldSerialize = obj => (shouldSerialize == null || shouldSerialize(obj)) && !property.IsValueEmptyCollection(obj);
            return property;
        }

        protected override JsonDictionaryContract CreateDictionaryContract(Type objectType) {
            var contract = base.CreateDictionaryContract(objectType);
            if (_useDefaultContract != null && _useDefaultContract(objectType))
                contract.DictionaryKeyResolver = propertyName => propertyName;

            return contract;
        }

        protected override string ResolvePropertyName(string propertyName) {
            return propertyName.ToLowerUnderscoredWords();
        }
    }
}