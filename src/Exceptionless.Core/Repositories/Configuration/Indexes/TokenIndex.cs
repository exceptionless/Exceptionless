using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class TokenIndex : VersionedIndex<Models.Token>
{
    internal const string KEYWORD_LOWERCASE_ANALYZER = "keyword_lowercase";
    private readonly ExceptionlessElasticConfiguration _configuration;

    public TokenIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "tokens", 1)
    {
        _configuration = configuration;
    }

    public override void ConfigureIndexMapping(TypeMappingDescriptor<Models.Token> map)
    {
        map
            .Dynamic(DynamicMapping.False)
            .Properties(p => p
                .SetupDefaults()
                .Date(e => e.ExpiresUtc)
                .Keyword(e => e.OrganizationId)
                .Keyword(e => e.ProjectId)
                .Keyword(e => e.DefaultProjectId)
                .Keyword(e => e.UserId)
                .Keyword(e => e.CreatedBy)
                .Keyword(e => e.Refresh)
                .Keyword(e => e.Scopes)
                .Boolean(e => e.IsDisabled)
                .Boolean(e => e.IsSuspended)
                .IntegerNumber(e => e.Type));
    }

    public override void ConfigureIndex(CreateIndexRequestDescriptor idx)
    {
        base.ConfigureIndex(idx);
        idx.Settings(s => s
            .Analysis(d => d.Analyzers(b => b.Custom(KEYWORD_LOWERCASE_ANALYZER, c => c.Filter("lowercase").Tokenizer("keyword"))))
            .NumberOfShards(_configuration.Options.NumberOfShards)
            .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
            .Priority(10));
    }
}
