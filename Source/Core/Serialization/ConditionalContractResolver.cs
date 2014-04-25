using System;
using System.Reflection;
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
    class ConditionalContractResolver : DefaultContractResolver {
        protected Func<JsonProperty, bool> _includeProperty;

        public ConditionalContractResolver(Func<JsonProperty, bool> includeProperty) {
            _includeProperty = includeProperty;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
            JsonProperty property = base.CreateProperty(member, memberSerialization);
            if (_includeProperty == null)
                return property;
            
            Predicate<object> shouldSerialize = property.ShouldSerialize;
            property.ShouldSerialize = obj => _includeProperty(property) && (shouldSerialize == null || shouldSerialize(obj));
            return property;
        }
    }
}