using Elastic.Clients.Elasticsearch;
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

    public OrganizationIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "organizations", 3)
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
                .FloatNumber(e => e.BillingStatus)
                .DoubleNumber(e => e.BillingPrice)
                .Boolean(e => e.IsSuspended)
                .IntegerNumber(e => e.RetentionDays)
                .Object(e => e.Invites, o => o.Properties(ip => ip
                    .Keyword("token")
                    .Text("email_address", t => t.Analyzer(KEYWORD_LOWERCASE_ANALYZER))))
                .Date(e => e.LastEventDateUtc)
                .Date(e => e.LastAppliedUsageBucketUtc)
                .Object(e => e.AssistantUsage, usage => usage.Properties(new Properties
                {
                    [new PropertyName((AssistantUsageInfo e) => e.Date)] = new DateProperty(),
                    [new PropertyName((AssistantUsageInfo e) => e.PlanId!)] = new KeywordProperty(),
                    [new PropertyName((AssistantUsageInfo e) => e.Turns)] = new LongNumberProperty(),
                    [new PropertyName((AssistantUsageInfo e) => e.Completed)] = new LongNumberProperty(),
                    [new PropertyName((AssistantUsageInfo e) => e.Failed)] = new LongNumberProperty(),
                    [new PropertyName((AssistantUsageInfo e) => e.Cancelled)] = new LongNumberProperty(),
                    [new PropertyName((AssistantUsageInfo e) => e.ProviderRequests)] = new LongNumberProperty(),
                    [new PropertyName((AssistantUsageInfo e) => e.ToolCalls)] = new LongNumberProperty(),
                    [new PropertyName((AssistantUsageInfo e) => e.PromptTokens)] = new LongNumberProperty(),
                    [new PropertyName((AssistantUsageInfo e) => e.CompletionTokens)] = new LongNumberProperty(),
                    [new PropertyName((AssistantUsageInfo e) => e.CostInMicrodollars)] = new LongNumberProperty(),
                    [new PropertyName((AssistantUsageInfo e) => e.BlockedByConcurrency)] = new LongNumberProperty(),
                    [new PropertyName((AssistantUsageInfo e) => e.BlockedByRateLimit)] = new LongNumberProperty(),
                    [new PropertyName((AssistantUsageInfo e) => e.BlockedByTokenLimit)] = new LongNumberProperty(),
                    [new PropertyName((AssistantUsageInfo e) => e.BlockedByCostLimit)] = new LongNumberProperty(),
                    [new PropertyName((AssistantUsageInfo e) => e.LastUsedUtc)] = new DateProperty()
                 }))
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
                .FloatNumber("total")
                .FloatNumber("blocked")
                .FloatNumber("discarded")
                .FloatNumber("limit")
                .FloatNumber("too_big")
                .FloatNumber("deleted")))
            .Object(o => o.UsageHours, ui => ui.Properties(p => p
                .Date("date")
                .FloatNumber("total")
                .FloatNumber("blocked")
                .FloatNumber("discarded")
                .FloatNumber("limit")
                .FloatNumber("too_big")
                .FloatNumber("deleted")));

    }
}
