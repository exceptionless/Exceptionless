using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class SavedViewIndex : VersionedIndex<Models.SavedView>
{
    internal const string KEYWORD_LOWERCASE_ANALYZER = "keyword_lowercase";

    private readonly ExceptionlessElasticConfiguration _configuration;

    public SavedViewIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "saved-views", 1)
    {
        _configuration = configuration;
    }

    public override TypeMappingDescriptor<Models.SavedView> ConfigureIndexMapping(TypeMappingDescriptor<Models.SavedView> map)
    {
        return map
            .Dynamic(false)
            .Properties(p => p
                .SetupDefaults()
                .Keyword(f => f.Name(e => e.OrganizationId))
                .Keyword(f => f.Name(e => e.UserId))
                .Keyword(f => f.Name(e => e.CreatedByUserId))
                .Keyword(f => f.Name(e => e.UpdatedByUserId))
                .Text(f => f.Name(e => e.Name).Analyzer(KEYWORD_LOWERCASE_ANALYZER).AddKeywordField())
                .Keyword(f => f.Name(e => e.View))
                .Boolean(f => f.Name(e => e.IsDefault))
                .Number(f => f.Name(e => e.Version).Type(NumberType.Integer)));
    }

    public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx)
    {
        return base.ConfigureIndex(idx.Settings(s => s
            .Analysis(d => d.Analyzers(b => b.Custom(KEYWORD_LOWERCASE_ANALYZER, c => c.Filters("lowercase").Tokenizer("keyword"))))
            .NumberOfShards(_configuration.Options.NumberOfShards)
            .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
            .Priority(5)));
    }
}
