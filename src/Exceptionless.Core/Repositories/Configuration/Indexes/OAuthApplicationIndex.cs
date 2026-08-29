using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Models;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class OAuthApplicationIndex : VersionedIndex<OAuthApplication>
{
    private readonly ExceptionlessElasticConfiguration _configuration;

    public OAuthApplicationIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "oauth-applications", 2)
    {
        _configuration = configuration;
    }

    public override void ConfigureIndexMapping(TypeMappingDescriptor<OAuthApplication> map)
    {
        map
            .Dynamic(DynamicMapping.False)
            .Properties(p => p
                .SetupDefaults()
                .Text(e => e.Name, t => t.AddKeywordField())
                .Keyword(e => e.ClientId)
                .Keyword(e => e.RedirectUris)
                .Keyword(e => e.Scopes)
                .Keyword(e => e.OrganizationIds)
                .Keyword(e => e.CreatedByUserId)
                .Keyword(e => e.UpdatedByUserId)
                .Boolean(e => e.IsDisabled));
    }

    public override void ConfigureIndex(CreateIndexRequestDescriptor idx)
    {
        base.ConfigureIndex(idx);
        idx.Settings(s => s
            .NumberOfShards(_configuration.Options.NumberOfShards)
            .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
            .Priority(5));
    }
}
