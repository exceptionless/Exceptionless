using System;
using System.Collections.Generic;
using System.Reflection;
using Elasticsearch.Net;
using Exceptionless.Core.Serialization;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Exceptionless.Serializer {
    public class ElasticDynamicTypeContractResolver : ElasticContractResolver {
        private readonly HashSet<Assembly> _assemblies = new HashSet<Assembly>();

        private readonly ElasticContractResolver _defaultResolver;
        private readonly ElasticContractResolver _resolver;

        public ElasticDynamicTypeContractResolver(IConnectionSettingsValues connectionSettings, IList<Func<Type, JsonConverter>> contractConverters) : base(connectionSettings, contractConverters) {
            _resolver = new ElasticLowerCaseUnderscorePropertyNamesContractResolver(connectionSettings, contractConverters);
            _defaultResolver = new ElasticContractResolver(connectionSettings, contractConverters);

            _assemblies.Add(typeof(ElasticsearchDefaultSerializer).Assembly);
            _assemblies.Add(typeof(ElasticContractResolver).Assembly);
        }

        public override JsonContract ResolveContract(Type type) {
            if (_assemblies.Contains(type.Assembly))
                return _defaultResolver.ResolveContract(type);

            return _resolver.ResolveContract(type);
        }
    }
}