using System;
using System.Reflection;
using Exceptionless.Models;
#if EMBEDDED
using Exceptionless.Json;
using Exceptionless.Json.Serialization;
using Exceptionless.Extensions;
#else
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Exceptionless.Core.Extensions;
#endif

namespace Exceptionless.Serializer {
#if EMBEDDED
    internal
#else
    public
#endif
    class ExceptionlessContractResolver : DefaultContractResolver {
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

#if EMBEDDED
        protected internal
#else
        protected
#endif
        override string ResolvePropertyName(string propertyName) {
            return propertyName.ToLowerUnderscoredWords();
        }
    }
}