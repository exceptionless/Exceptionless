using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class SavedViewIndex : VersionedIndex<Models.SavedView>
{
    internal const string KEYWORD_LOWERCASE_ANALYZER = "keyword_lowercase";

    private readonly ExceptionlessElasticConfiguration _configuration;

    public SavedViewIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "saved-views", 1)
    {
        _configuration = configuration;
    }

    public override void ConfigureIndexMapping(TypeMappingDescriptor<Models.SavedView> map)
    {
        map
            .Dynamic(DynamicMapping.False)
            .Properties(p => p
                .SetupDefaults()
                .Keyword(e => e.OrganizationId)
                .Keyword(e => e.UserId)
                .Keyword(e => e.CreatedByUserId)
                .Keyword(e => e.UpdatedByUserId)
                .Text(e => e.Name, t => t.Analyzer(KEYWORD_LOWERCASE_ANALYZER).AddKeywordField())
                .Keyword(e => e.ViewType)
                .Boolean(e => e.IsDefault)
                .IntegerNumber(e => e.Version));
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
