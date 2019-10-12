using System;
using System.Collections.Generic;
using System.Linq;
using Elasticsearch.Net;
using Nest;
using Nest.JsonNetSerializer;
using Newtonsoft.Json;

namespace Exceptionless.Core.Serialization {
    public class ElasticJsonNetSerializer : JsonNetSerializer {
        private readonly JsonSerializerSettings _serializerSettings;

        public ElasticJsonNetSerializer(
            IElasticsearchSerializer builtinSerializer, 
            IConnectionSettingsValues connectionSettings,
            JsonSerializerSettings serializerSettings
        ) : base(
            builtinSerializer, 
            connectionSettings
        ) {
            _serializerSettings = serializerSettings;
        }

        protected override ConnectionSettingsAwareContractResolver CreateContractResolver() {
            // TODO: Verify we don't need to use the DynamicTypeContractResolver.
            var resolver = new ElasticConnectionSettingsAwareContractResolver(ConnectionSettings);
            ModifyContractResolver(resolver);
            return resolver;
        }

        protected override JsonSerializerSettings CreateJsonSerializerSettings() {
            return new JsonSerializerSettings {
                DateParseHandling = _serializerSettings.DateParseHandling,
                DefaultValueHandling = _serializerSettings.DefaultValueHandling,
                MissingMemberHandling = _serializerSettings.MissingMemberHandling,
                NullValueHandling = _serializerSettings.NullValueHandling
            };
        }

        protected override IEnumerable<JsonConverter> CreateJsonConverters() {
            return _serializerSettings.Converters.ToList();
        }
    }
}
