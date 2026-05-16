using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Models;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class OrganizationIndex : VersionedIndex<Organization>
{
    private const string KEYWORD_LOWERCASE_ANALYZER = "keyword_lowercase";
    private readonly ExceptionlessElasticConfiguration _configuration;

    public OrganizationIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "organizations", 2)
    {
        _configuration = configuration;
    }

    public override void ConfigureIndexMapping(TypeMappingDescriptor<Organization> map)
    {
        map
            .Dynamic(DynamicMapping.False)
            .Properties(p => p
                .SetupDefaults()
                .Text(e => e.Name, t => t.AddKeywordField())
                .Keyword(e => e.StripeCustomerId)
                .Boolean(e => e.HasPremiumFeatures)
                .Keyword(e => e.Features)
                .Keyword(e => e.PlanId)
                .Keyword(e => e.PlanName, k => k.IgnoreAbove(256))
                .Date(e => e.SubscribeDate)
                .IntegerNumber(e => e.BillingStatus)
                .DoubleNumber(e => e.BillingPrice)
                .Boolean(e => e.IsSuspended)
                .IntegerNumber(e => e.RetentionDays)
                .Object(e => e.Invites, o => o.Properties(ip => ip
                    .Keyword("token")
                    .Text("email_address", t => t.Analyzer(KEYWORD_LOWERCASE_ANALYZER))))
                .Date(e => e.LastEventDateUtc)
                .AddUsageMappings());
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

internal static class OrganizationIndexExtensions
{
    public static PropertiesDescriptor<Organization> AddUsageMappings(this PropertiesDescriptor<Organization> descriptor)
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
