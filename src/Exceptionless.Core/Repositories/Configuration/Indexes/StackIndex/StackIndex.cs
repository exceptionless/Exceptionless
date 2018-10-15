using System;
using Exceptionless.Core.Configuration;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Microsoft.Extensions.Options;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class StackIndex : VersionedIndex {
        private readonly IOptions<ElasticsearchOptions> _elasticsearchOptions;

        public StackIndex(ExceptionlessElasticConfiguration configuration, IOptions<AppOptions> appOptions) : base(configuration, appOptions.Value.ScopePrefix + "stacks", 1) {
            _elasticsearchOptions = configuration.ElasticsearchOptions;
            AddType(Stack = new StackIndexType(this));
        }

        public StackIndexType Stack { get; }

        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .NumberOfShards(_elasticsearchOptions.Value.NumberOfShards)
                .NumberOfReplicas(_elasticsearchOptions.Value.NumberOfReplicas)
                .Priority(5)));
        }
    }
}