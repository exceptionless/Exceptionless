using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class OAuthTokenIndex : VersionedIndex<OAuthToken>
{
    private readonly ExceptionlessElasticConfiguration _configuration;

    public OAuthTokenIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "oauth-tokens", 1)
    {
        _configuration = configuration;
    }

    public override void ConfigureIndexMapping(TypeMappingDescriptor<OAuthToken> map)
    {
        map
            .Dynamic(DynamicMapping.False)
            .Properties(p => p
                .SetupDefaults()
                .Keyword(e => e.UserId)
                .Keyword(e => e.ClientId)
                .Keyword(e => e.GrantId)
                .Keyword(e => e.Resource)
                .Keyword(e => e.AccessTokenHash)
                .Keyword(e => e.RefreshTokenHash)
                .Keyword(e => e.OrganizationIds)
                .Keyword(e => e.Scopes)
                .Keyword(e => e.CreatedBy)
                .Date(e => e.ExpiresUtc)
                .Date(e => e.RefreshExpiresUtc)
                .Boolean(e => e.IsDisabled)
                .Boolean(e => e.IsSuspended));
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
