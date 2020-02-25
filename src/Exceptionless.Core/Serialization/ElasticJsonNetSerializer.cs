using System.Linq;
using Elasticsearch.Net;
using Nest;
using Nest.JsonNetSerializer;
using Newtonsoft.Json;

namespace Exceptionless.Core.Serialization {
    public class ElasticJsonNetSerializer : JsonNetSerializer {
        public ElasticJsonNetSerializer(
            IElasticsearchSerializer builtinSerializer, 
            IConnectionSettingsValues connectionSettings,
            JsonSerializerSettings serializerSettings
        ) : base(
            builtinSerializer, 
            connectionSettings,
            () => CreateJsonSerializerSettings(serializerSettings),
            contractJsonConverters: serializerSettings.Converters.ToList()
        ) {
        }

        private static JsonSerializerSettings CreateJsonSerializerSettings(JsonSerializerSettings serializerSettings) {
            return new JsonSerializerSettings {
                DateParseHandling = serializerSettings.DateParseHandling,
                DefaultValueHandling = serializerSettings.DefaultValueHandling,
                MissingMemberHandling = serializerSettings.MissingMemberHandling,
                NullValueHandling = serializerSettings.NullValueHandling
            };
        }

        protected override ConnectionSettingsAwareContractResolver CreateContractResolver() {
            // TODO: Verify we don't need to use the DynamicTypeContractResolver.
            var resolver = new ElasticConnectionSettingsAwareContractResolver(ConnectionSettings);
            ModifyContractResolver(resolver);
            return resolver;
        }
    }
}
