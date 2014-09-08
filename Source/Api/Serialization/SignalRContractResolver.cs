using System;
using System.Reflection;
using Exceptionless.Core.Serialization;
using Microsoft.AspNet.SignalR.Infrastructure;
using Newtonsoft.Json.Serialization;

namespace Exceptionless.Api.Serialization {
    public class SignalRContractResolver : IContractResolver {
        private readonly Assembly _assembly;
        private readonly IContractResolver _camelCaseContractResolver;
        private readonly IContractResolver _defaultContractSerializer;

        public SignalRContractResolver() {
            _defaultContractSerializer = new DefaultContractResolver();
            _camelCaseContractResolver = new LowerCaseUnderscorePropertyNamesContractResolver();
            _assembly = typeof(Connection).Assembly;
        }

        public JsonContract ResolveContract(Type type) {
            if (type.Assembly.Equals(_assembly))
                return _defaultContractSerializer.ResolveContract(type);

            return _camelCaseContractResolver.ResolveContract(type);
        }
    }
}