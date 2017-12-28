using System;
using System.Collections.Generic;
using System.Reflection;
using Exceptionless.Core.Extensions;
using Newtonsoft.Json.Serialization;

namespace Exceptionless.Serializer {
    public class DynamicTypeContractResolver : IContractResolver {
        private readonly HashSet<Assembly> _assemblies = new HashSet<Assembly>();
        private readonly HashSet<Type> _types = new HashSet<Type>();

        private readonly IContractResolver _defaultResolver;
        private readonly IContractResolver _resolver;

        public DynamicTypeContractResolver(IContractResolver resolver, IContractResolver defaultResolver = null) {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _defaultResolver = defaultResolver ?? new DefaultContractResolver();
        }

        public void UseDefaultResolverFor(params Assembly[] assemblies) {
            _assemblies.AddRange(assemblies);
        }

        public void UseDefaultResolverFor(params Type[] types) {
            _types.AddRange(types);
        }

        public JsonContract ResolveContract(Type type) {
            if (_types.Contains(type) || _assemblies.Contains(type.Assembly))
                return _defaultResolver.ResolveContract(type);

            return _resolver.ResolveContract(type);
        }
    }
}