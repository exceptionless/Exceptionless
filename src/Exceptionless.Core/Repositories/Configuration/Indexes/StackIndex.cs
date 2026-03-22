using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Parsers.ElasticQueries;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class StackIndex : VersionedIndex<Stack>
{
    private const string COMMA_WHITESPACE_ANALYZER = "comma_whitespace";
    private const string STANDARDPLUS_ANALYZER = "standardplus";
    private const string WHITESPACE_LOWERCASE_ANALYZER = "whitespace_lower";
    private const string COMMA_WHITESPACE_TOKENIZER = "comma_whitespace";

    private readonly ExceptionlessElasticConfiguration _configuration;

    public StackIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "stacks", 1)
    {
        _configuration = configuration;
    }

    public override void ConfigureIndex(CreateIndexRequestDescriptor idx)
    {
        base.ConfigureIndex(idx);
        idx.Settings(s => s
            .Analysis(a => BuildAnalysis(a))
            .NumberOfShards(_configuration.Options.NumberOfShards)
            .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
            .Priority(5));
    }

    public override void ConfigureIndexMapping(TypeMappingDescriptor<Stack> map)
    {
        map
            .Dynamic(DynamicMapping.False)
            .Properties(p => p
                .SetupDefaults()
                .Keyword(e => e.OrganizationId, k => k.IgnoreAbove(1024))
                    .FieldAlias(Alias.OrganizationId, a => a.Path(f => f.OrganizationId))
                .Keyword(e => e.ProjectId, k => k.IgnoreAbove(1024))
                    .FieldAlias(Alias.ProjectId, a => a.Path(f => f.ProjectId))
                .Keyword(e => e.Status)
                .Date(e => e.SnoozeUntilUtc)
                .Keyword(e => e.SignatureHash, k => k.IgnoreAbove(1024))
                    .FieldAlias(Alias.SignatureHash, a => a.Path(f => f.SignatureHash))
                .Keyword(e => e.DuplicateSignature)
                .Keyword(e => e.Type, k => k.IgnoreAbove(1024))
                .Date(e => e.FirstOccurrence)
                    .FieldAlias(Alias.FirstOccurrence, a => a.Path(f => f.FirstOccurrence))
                .Date(e => e.LastOccurrence)
                    .FieldAlias(Alias.LastOccurrence, a => a.Path(f => f.LastOccurrence))
                .Text(e => e.Title)
                .Text(e => e.Description)
                .Keyword(e => e.Tags, k => k.IgnoreAbove(1024))
                    .FieldAlias(Alias.Tags, a => a.Path(f => f.Tags))
                .Keyword(e => e.References, k => k.IgnoreAbove(1024))
                    .FieldAlias(Alias.References, a => a.Path(f => f.References))
                .Date(e => e.DateFixed)
                    .FieldAlias(Alias.DateFixed, a => a.Path(f => f.DateFixed))
                .Boolean(Alias.IsFixed)
                .Keyword(e => e.FixedInVersion, k => k.IgnoreAbove(1024))
                    .FieldAlias(Alias.FixedInVersion, a => a.Path(f => f.FixedInVersion))
                .Boolean(e => e.OccurrencesAreCritical)
                    .FieldAlias(Alias.OccurrencesAreCritical, a => a.Path(f => f.OccurrencesAreCritical))
                .IntegerNumber(e => e.TotalOccurrences)
                    .FieldAlias(Alias.TotalOccurrences, a => a.Path(f => f.TotalOccurrences))
            );
    }

    protected override void ConfigureQueryParser(ElasticQueryParserConfiguration config)
    {
        string dateFixedFieldName = InferPropertyName(f => f.DateFixed);
        config
            .SetDefaultFields(["id", Alias.Title, Alias.Description, Alias.Tags, Alias.References])
            .AddVisitor(new StackDateFixedQueryVisitor(dateFixedFieldName))
            .UseFieldMap(new Dictionary<string, string> {
                    { Alias.Stack, "id" }
            });
    }

    private void BuildAnalysis(IndexSettingsAnalysisDescriptor ad)
    {
        ad.Analyzers(a => a
                .Pattern(COMMA_WHITESPACE_ANALYZER, p => p.Pattern(@"[,\s]+"))
                .Custom(STANDARDPLUS_ANALYZER, c => c.Filter("lowercase", "stop", "unique").Tokenizer(COMMA_WHITESPACE_TOKENIZER))
                .Custom(WHITESPACE_LOWERCASE_ANALYZER, c => c.Filter("lowercase").Tokenizer("whitespace")))
            .Tokenizers(t => t
                .Pattern(COMMA_WHITESPACE_TOKENIZER, p => p.Pattern(@"[,\s]+")));
    }

    public class Alias
    {
        public const string Stack = "stack";
        public const string Status = "status";
        public const string OrganizationId = "organization";
        public const string ProjectId = "project";
        public const string SignatureHash = "signature";
        public const string Type = "type";
        public const string FirstOccurrence = "first";
        public const string LastOccurrence = "last";
        public const string Title = "title";
        public const string Description = "description";
        public const string Tags = "tag";
        public const string References = "links";
        public const string DateFixed = "fixedon";
        public const string IsFixed = "fixed";
        public const string FixedInVersion = "version_fixed";
        public const string IsHidden = "hidden";
        public const string IsRegressed = "regressed";
        public const string OccurrencesAreCritical = "critical";
        public const string TotalOccurrences = "occurrences";
    }
}
