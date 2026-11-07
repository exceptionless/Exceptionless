using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class TrialClaimIndex : VersionedIndex<TrialClaim>
{
    private readonly ExceptionlessElasticConfiguration _configuration;

    public TrialClaimIndex(ExceptionlessElasticConfiguration configuration)
        : base(configuration, configuration.Options.ScopePrefix + "trial-claims", 1)
    {
        _configuration = configuration;
    }

    public override void ConfigureIndexMapping(TypeMappingDescriptor<TrialClaim> map)
    {
        map
            .Dynamic(DynamicMapping.False)
            .Properties(properties => properties.SetupDefaults());
    }

    public override void ConfigureIndex(CreateIndexRequestDescriptor index)
    {
        base.ConfigureIndex(index);
        index.Settings(settings => settings
            .NumberOfShards(1)
            .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
            .Priority(5));
    }
}
