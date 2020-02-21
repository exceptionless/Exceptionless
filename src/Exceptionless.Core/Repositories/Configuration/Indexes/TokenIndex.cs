using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class TokenIndex : VersionedIndex<Models.Token> {
        internal const string KEYWORD_LOWERCASE_ANALYZER = "keyword_lowercase";
        private readonly ExceptionlessElasticConfiguration _configuration;

        public TokenIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "tokens", 1) {
            _configuration = configuration;
        }

        public override TypeMappingDescriptor<Models.Token> ConfigureIndexMapping(TypeMappingDescriptor<Models.Token> map) {
            return map
                .Dynamic(false)
                .Properties(p => p
                    .SetupDefaults()
                    .Date(f => f.Name(e => e.ExpiresUtc))
                    .Keyword(f => f.Name(e => e.OrganizationId))
                    .Keyword(f => f.Name(e => e.ProjectId))
                    .Keyword(f => f.Name(e => e.DefaultProjectId))
                    .Keyword(f => f.Name(e => e.UserId))
                    .Keyword(f => f.Name(u => u.CreatedBy))
                    .Keyword(f => f.Name(e => e.Refresh))
                    .Keyword(f => f.Name(e => e.Scopes))
                    .Boolean(f => f.Name(e => e.IsDisabled))
                    .Number(f => f.Name(e => e.Type).Type(NumberType.Byte)));
        }

        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .Analysis(d => d.Analyzers(b => b.Custom(KEYWORD_LOWERCASE_ANALYZER, c => c.Filters("lowercase").Tokenizer("keyword"))))
                .NumberOfShards(_configuration.Options.NumberOfShards)
                .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
                .Priority(10)));
        }
    }
}