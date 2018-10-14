using System;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class StackIndex : VersionedIndex {
        private readonly AppOptions _appOptions;

        public StackIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.AppOptions.AppScopePrefix + "stacks", 1) {
            _appOptions = configuration.AppOptions;
            AddType(Stack = new StackIndexType(this));
        }

        public StackIndexType Stack { get; }

        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .NumberOfShards(_appOptions.ParsedElasticsearchConnectionString.NumberOfShards)
                .NumberOfReplicas(_appOptions.ParsedElasticsearchConnectionString.NumberOfReplicas)
                .Priority(5)));
        }
    }
}