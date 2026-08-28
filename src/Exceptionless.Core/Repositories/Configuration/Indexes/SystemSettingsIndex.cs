using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class SystemSettingsIndex : VersionedIndex<SystemSettings>
{
    private readonly ExceptionlessElasticConfiguration _configuration;

    public SystemSettingsIndex(ExceptionlessElasticConfiguration configuration)
        : base(configuration, configuration.Options.ScopePrefix + "system-settings", 1)
    {
        _configuration = configuration;
    }

    public override void ConfigureIndexMapping(TypeMappingDescriptor<SystemSettings> map)
    {
        map
            .Dynamic(DynamicMapping.False)
            .Properties(properties => properties
                .SetupDefaults()
                .Keyword(settings => settings.AssistantModel)
                .Boolean(settings => settings.AssistantEnabled)
                .Boolean(settings => settings.EventSubmissionEnabled)
                .Object(settings => settings.SystemNotification, notification => notification.Properties(properties => properties
                    .Date("date")
                    .Text("message")
                    .Keyword("level")
                    .Keyword("target")))
                .Keyword(settings => settings.CreatedByUserId)
                .Keyword(settings => settings.UpdatedByUserId));
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
