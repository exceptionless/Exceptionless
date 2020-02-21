using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class WebHookIndex : VersionedIndex<WebHook> {
        private readonly ExceptionlessElasticConfiguration _configuration;

        public WebHookIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "webhooks", 1) {
            _configuration = configuration;
        }

        public override TypeMappingDescriptor<WebHook> ConfigureIndexMapping(TypeMappingDescriptor<WebHook> map) {
            return map
                .Dynamic(false)
                .Properties(p => p
                    .SetupDefaults()
                    .Keyword(f => f.Name(e => e.OrganizationId))
                    .Keyword(f => f.Name(e => e.ProjectId))
                    .Keyword(f => f.Name(e => e.Url))
                    .Keyword(f => f.Name(e => e.EventTypes))
                    .Boolean(f => f.Name(e => e.IsEnabled))
                );
        }

        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .NumberOfShards(_configuration.Options.NumberOfShards)
                .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
                .Priority(5)));
        }
    }
}