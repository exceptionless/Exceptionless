using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Models;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class ProjectIndex : VersionedIndex<Project>
{
    internal const string KEYWORD_LOWERCASE_ANALYZER = "keyword_lowercase";
    private readonly ExceptionlessElasticConfiguration _configuration;

    public ProjectIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "projects", 2)
    {
        _configuration = configuration;
    }

    public override void ConfigureIndexMapping(TypeMappingDescriptor<Project> map)
    {
        map
            .Dynamic(DynamicMapping.False)
            .Properties(p => p
                .SetupDefaults()
                .Keyword(e => e.OrganizationId)
                .Text(e => e.Name, t => t.AddKeywordField())
                .LongNumber(e => e.NextSummaryEndOfDayTicks)
                .Date(e => e.LastEventDateUtc)
                .AddUsageMappings()
            );
    }

    public override void ConfigureIndex(CreateIndexRequestDescriptor idx)
    {
        base.ConfigureIndex(idx);
        idx.Settings(s => s
            .Analysis(d => d.Analyzers(b => b.Custom(KEYWORD_LOWERCASE_ANALYZER, c => c.Filter("lowercase").Tokenizer("keyword"))))
            .NumberOfShards(_configuration.Options.NumberOfShards)
            .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
            .Priority(10));
    }
}

internal static class ProjectIndexExtensions
{
    public static PropertiesDescriptor<Project> AddUsageMappings(this PropertiesDescriptor<Project> descriptor)
    {
        return descriptor
            .Object(o => o.Usage, ui => ui.Properties(p => p
                .Date("date")
                .IntegerNumber("total")
                .IntegerNumber("blocked")
                .IntegerNumber("discarded")
                .IntegerNumber("limit")
                .IntegerNumber("too_big")))
            .Object(o => o.UsageHours, ui => ui.Properties(p => p
                .Date("date")
                .IntegerNumber("total")
                .IntegerNumber("blocked")
                .IntegerNumber("discarded")
                .IntegerNumber("too_big")));
    }
}
