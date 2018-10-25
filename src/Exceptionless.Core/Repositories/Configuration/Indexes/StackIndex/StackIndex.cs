using System;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class StackIndex : VersionedIndex {
        private readonly ExceptionlessElasticConfiguration _configuration;

        public StackIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "stacks", 1) {
            _configuration = configuration;
            AddType(Stack = new StackIndexType(this));
        }

        public StackIndexType Stack { get; }

        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .NumberOfShards(_configuration.Options.NumberOfShards)
                .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
                .Priority(5)));
        }
    }
}