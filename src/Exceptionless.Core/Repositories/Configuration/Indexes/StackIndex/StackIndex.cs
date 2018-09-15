using System;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class StackIndex : VersionedIndex {
        private readonly Settings _settings;

        public StackIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Settings.AppScopePrefix + "stacks", 1) {
            _settings = configuration.Settings;
            AddType(Stack = new StackIndexType(this));
        }

        public StackIndexType Stack { get; }

        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .NumberOfShards(_settings.ParsedElasticsearchConnectionString.NumberOfShards)
                .NumberOfReplicas(_settings.ParsedElasticsearchConnectionString.NumberOfReplicas)
                .Priority(5)));
        }
    }
}