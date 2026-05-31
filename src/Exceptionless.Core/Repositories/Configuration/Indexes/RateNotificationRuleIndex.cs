using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class RateNotificationRuleIndex : VersionedIndex<RateNotificationRule>
{
    private readonly ExceptionlessElasticConfiguration _configuration;

    public RateNotificationRuleIndex(ExceptionlessElasticConfiguration configuration)
        : base(configuration, configuration.Options.ScopePrefix + "rate-notification-rules", 1)
    {
        _configuration = configuration;
    }

    public override TypeMappingDescriptor<RateNotificationRule> ConfigureIndexMapping(TypeMappingDescriptor<RateNotificationRule> map)
    {
        return map
            .Dynamic(false)
            .Properties(p => p
                .SetupDefaults()
                .Keyword(f => f.Name(e => e.OrganizationId))
                .Keyword(f => f.Name(e => e.ProjectId))
                .Keyword(f => f.Name(e => e.UserId))
                .Keyword(f => f.Name(e => e.StackId))
                .Keyword(f => f.Name(e => e.Signal))
                .Keyword(f => f.Name(e => e.Subject))
                .Boolean(f => f.Name(e => e.IsEnabled))
                .Boolean(f => f.Name(e => e.IsDeleted))
                .Text(f => f.Name(e => e.Name).AddKeywordField())
            );
    }

    public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx)
    {
        return base.ConfigureIndex(idx.Settings(s => s
            .NumberOfShards(_configuration.Options.NumberOfShards)
            .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
            .Priority(5)));
    }
}
