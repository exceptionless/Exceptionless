using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Parsers.ElasticQueries;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class StackIndex : VersionedIndex<Stack> {
        private const string COMMA_WHITESPACE_ANALYZER = "comma_whitespace";
        private const string STANDARDPLUS_ANALYZER = "standardplus";
        private const string WHITESPACE_LOWERCASE_ANALYZER = "whitespace_lower";
        private const string COMMA_WHITESPACE_TOKENIZER = "comma_whitespace";
        
        private readonly ExceptionlessElasticConfiguration _configuration;

        public StackIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "stacks", 1) {
            _configuration = configuration;
        }
        
        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .Analysis(BuildAnalysis)
                .NumberOfShards(_configuration.Options.NumberOfShards)
                .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
                .Priority(5)));
        }
        
        public override TypeMappingDescriptor<Stack> ConfigureIndexMapping(TypeMappingDescriptor<Stack> map) {
            return map
                .Dynamic(false)
                .Properties(p => p
                    .SetupDefaults()
                    .Keyword(f => f.Name(s => s.OrganizationId).IgnoreAbove(1024))
                        .FieldAlias(a => a.Name(Alias.OrganizationId).Path(f => f.OrganizationId))
                    .Keyword(f => f.Name(s => s.ProjectId).IgnoreAbove(1024))
                        .FieldAlias(a => a.Name(Alias.ProjectId).Path(f => f.ProjectId))
                    .Keyword(f => f.Name(s => s.SignatureHash).IgnoreAbove(1024))
                        .FieldAlias(a => a.Name(Alias.SignatureHash).Path(f => f.SignatureHash))
                    .Keyword(f => f.Name(e => e.Type).IgnoreAbove(1024))
                    .Date(f => f.Name(s => s.FirstOccurrence))
                        .FieldAlias(a => a.Name(Alias.FirstOccurrence).Path(f => f.FirstOccurrence))
                    .Date(f => f.Name(s => s.LastOccurrence))
                        .FieldAlias(a => a.Name(Alias.LastOccurrence).Path(f => f.LastOccurrence))
                    .Text(f => f.Name(s => s.Title).Boost(1.1))
                    .Text(f => f.Name(s => s.Description))
                    .Keyword(f => f.Name(s => s.Tags).IgnoreAbove(1024).Boost(1.2))
                        .FieldAlias(a => a.Name(Alias.Tags).Path(f => f.Tags))
                    .Keyword(f => f.Name(s => s.References).IgnoreAbove(1024))
                        .FieldAlias(a => a.Name(Alias.References).Path(f => f.References))
                    .Date(f => f.Name(s => s.DateFixed))
                        .FieldAlias(a => a.Name(Alias.DateFixed).Path(f => f.DateFixed))
                    .Boolean(f => f.Name(Alias.IsFixed))
                    .Keyword(f => f.Name(s => s.FixedInVersion).IgnoreAbove(1024))
                        .FieldAlias(a => a.Name(Alias.FixedInVersion).Path(f => f.FixedInVersion))
                    .Boolean(f => f.Name(s => s.IsHidden))
                        .FieldAlias(a => a.Name(Alias.IsHidden).Path(f => f.IsHidden))
                    .Boolean(f => f.Name(s => s.IsRegressed))
                        .FieldAlias(a => a.Name(Alias.IsRegressed).Path(f => f.IsRegressed))
                    .Boolean(f => f.Name(s => s.OccurrencesAreCritical))
                        .FieldAlias(a => a.Name(Alias.OccurrencesAreCritical).Path(f => f.OccurrencesAreCritical))
                    .Scalar(f => f.TotalOccurrences)
                        .FieldAlias(a => a.Name(Alias.TotalOccurrences).Path(f => f.TotalOccurrences))
                );
        }

        protected override void ConfigureQueryParser(ElasticQueryParserConfiguration config) {
            string dateFixedFieldName = InferPropertyName(f => f.DateFixed);
            config
                .SetDefaultFields(new[] { "id", Alias.Title, Alias.Description, Alias.Tags, Alias.References })
                .AddVisitor(new StackDateFixedQueryVisitor(dateFixedFieldName));
        }
        
        private AnalysisDescriptor BuildAnalysis(AnalysisDescriptor ad) {
            return ad.Analyzers(a => a
                    .Pattern(COMMA_WHITESPACE_ANALYZER, p => p.Pattern(@"[,\s]+"))
                    .Custom(STANDARDPLUS_ANALYZER, c => c.Filters("lowercase", "stop", "unique").Tokenizer(COMMA_WHITESPACE_TOKENIZER))
                    .Custom(WHITESPACE_LOWERCASE_ANALYZER, c => c.Filters("lowercase").Tokenizer("whitespace")))
                .Tokenizers(t => t
                    .Pattern(COMMA_WHITESPACE_TOKENIZER, p => p.Pattern(@"[,\s]+")));
        }

        public class Alias {
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
}