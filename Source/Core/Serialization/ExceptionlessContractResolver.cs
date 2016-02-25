using System;
using System.Collections.Generic;
using System.Reflection;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Serialization;
using Newtonsoft.Json.Serialization;

namespace Exceptionless.Serializer {
    public class ExceptionlessContractResolver : DefaultContractResolver {
        private readonly HashSet<Assembly> _assemblies = new HashSet<Assembly>();
        private readonly HashSet<Type> _types = new HashSet<Type>();

        private readonly IContractResolver _defaultContractSerializer = new DefaultContractResolver();
        private readonly IContractResolver _camelCaseContractResolver;

        public ExceptionlessContractResolver() {
            _camelCaseContractResolver = new LowerCaseUnderscorePropertyNamesContractResolver();
        }

        public void UseDefaultResolverFor(params Assembly[] assemblies) {
            _assemblies.AddRange(assemblies);
        }

        public void UseDefaultResolverFor(params Type[] types) {
            _types.AddRange(types);
        }

        public override JsonContract ResolveContract(Type type) {
            if (_types.Contains(type) || _assemblies.Contains(type.Assembly))
                return _defaultContractSerializer.ResolveContract(type);

            return _camelCaseContractResolver.ResolveContract(type);
        }
    }
}