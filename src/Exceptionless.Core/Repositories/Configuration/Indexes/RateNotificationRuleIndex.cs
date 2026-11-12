using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Models;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class RateNotificationRuleIndex : VersionedIndex<RateNotificationRule>
{
    private readonly ExceptionlessElasticConfiguration _configuration;

    public RateNotificationRuleIndex(ExceptionlessElasticConfiguration configuration)
        : base(configuration, configuration.Options.ScopePrefix + "rate-notification-rules", 1)
    {
        _configuration = configuration;
    }

    public override void ConfigureIndexMapping(TypeMappingDescriptor<RateNotificationRule> map)
    {
        map
            .Dynamic(DynamicMapping.False)
            .Properties(p => p
                .SetupDefaults()
                .Keyword(e => e.OrganizationId)
                .Keyword(e => e.ProjectId)
                .Keyword(e => e.UserId)
                .Keyword(e => e.StackId)
                .Keyword(e => e.Signal)
                .Keyword(e => e.Subject)
                .Boolean(e => e.IsEnabled)
                .Boolean(e => e.IsDeleted)
                .Text(e => e.Name, t => t.AddKeywordField()));
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
