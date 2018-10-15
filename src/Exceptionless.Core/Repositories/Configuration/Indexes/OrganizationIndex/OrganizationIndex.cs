using System;
using Exceptionless.Core.Configuration;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Microsoft.Extensions.Options;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class OrganizationIndex : VersionedIndex {
        internal const string KEYWORD_LOWERCASE_ANALYZER = "keyword_lowercase";
        private readonly IOptions<ElasticsearchOptions> _elasticsearchOptions;

        public OrganizationIndex(ExceptionlessElasticConfiguration configuration, IOptions<AppOptions> appOptions) : base(configuration, appOptions.Value.ScopePrefix + "organizations", 1) {
            _elasticsearchOptions = configuration.ElasticsearchOptions;
            AddType(Organization = new OrganizationIndexType(this));
            AddType(Project = new ProjectIndexType(this));
            AddType(Token = new TokenIndexType(this));
            AddType(User = new UserIndexType(this));
            AddType(WebHook = new WebHookIndexType(this));
        }

        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .Analysis(d => d.Analyzers(b => b.Custom(KEYWORD_LOWERCASE_ANALYZER, c => c.Filters("lowercase").Tokenizer("keyword"))))
                .NumberOfShards(_elasticsearchOptions.Value.NumberOfShards)
                .NumberOfReplicas(_elasticsearchOptions.Value.NumberOfReplicas)
                .Priority(10)));
        }

        public OrganizationIndexType Organization { get; }
        public ProjectIndexType Project { get; }
        public TokenIndexType Token { get; }
        public UserIndexType User { get; }
        public WebHookIndexType WebHook { get; }
    }
}