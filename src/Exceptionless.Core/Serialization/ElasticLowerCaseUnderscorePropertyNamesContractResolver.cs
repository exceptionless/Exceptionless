using System;
using System.Collections.Generic;
using System.Reflection;
using Exceptionless.Core.Extensions;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Exceptionless.Core.Serialization {
    public class ElasticLowerCaseUnderscorePropertyNamesContractResolver : ElasticContractResolver {
        public ElasticLowerCaseUnderscorePropertyNamesContractResolver(IConnectionSettingsValues connectionSettings, IList<Func<Type, JsonConverter>> contractConverters) : base(connectionSettings, contractConverters) {}

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            Predicate<object> shouldSerialize = property.ShouldSerialize;
            property.ShouldSerialize = obj => (shouldSerialize == null || shouldSerialize(obj)) && !property.IsValueEmptyCollection(obj);
            return property;
        }

        protected override JsonDictionaryContract CreateDictionaryContract(Type objectType) {
            JsonDictionaryContract contract = base.CreateDictionaryContract(objectType);
            contract.DictionaryKeyResolver = propertyName => propertyName;
            return contract;
        }

        protected override string ResolvePropertyName(string propertyName) {
            return propertyName.ToLowerUnderscoredWords();
        }
    }
}