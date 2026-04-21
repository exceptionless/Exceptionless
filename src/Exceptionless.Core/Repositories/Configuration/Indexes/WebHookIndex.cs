using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class WebHookIndex : VersionedIndex<WebHook>
{
    private readonly ExceptionlessElasticConfiguration _configuration;

    public WebHookIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "webhooks", 1)
    {
        _configuration = configuration;
    }

    public override void ConfigureIndexMapping(TypeMappingDescriptor<WebHook> map)
    {
        map
            .Dynamic(DynamicMapping.False)
            .Properties(p => p
                .SetupDefaults()
                .Keyword(e => e.OrganizationId)
                .Keyword(e => e.ProjectId)
                .Keyword(e => e.Url)
                .Keyword(e => e.EventTypes)
                .Boolean(e => e.IsEnabled)
            );
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
