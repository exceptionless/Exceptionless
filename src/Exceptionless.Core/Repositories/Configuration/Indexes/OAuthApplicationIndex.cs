using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Models;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class OAuthApplicationIndex : VersionedIndex<OAuthApplication>
{
    internal const string KEYWORD_LOWERCASE_ANALYZER = "keyword_lowercase";
    private readonly ExceptionlessElasticConfiguration _configuration;

    public OAuthApplicationIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "oauth-applications", 1)
    {
        _configuration = configuration;
    }

    public override void ConfigureIndexMapping(TypeMappingDescriptor<OAuthApplication> map)
    {
        map
            .Dynamic(DynamicMapping.False)
            .Properties(p => p
                .SetupDefaults()
                .Text(e => e.Name, t => t.Analyzer(KEYWORD_LOWERCASE_ANALYZER).AddKeywordField())
                .Keyword(e => e.ClientId)
                .Keyword(e => e.RedirectUris)
                .Keyword(e => e.Scopes)
                .Keyword(e => e.GrantTypes)
                .Keyword(e => e.CreatedByUserId)
                .Keyword(e => e.UpdatedByUserId)
                .Boolean(e => e.IsDisabled));
    }

    public override void ConfigureIndex(CreateIndexRequestDescriptor idx)
    {
        base.ConfigureIndex(idx);
        idx.Settings(s => s
            .Analysis(d => d.Analyzers(b => b.Custom(KEYWORD_LOWERCASE_ANALYZER, c => c.Filter("lowercase").Tokenizer("keyword"))))
            .NumberOfShards(_configuration.Options.NumberOfShards)
            .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
            .Priority(5));
    }
}
