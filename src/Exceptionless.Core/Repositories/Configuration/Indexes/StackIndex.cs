using System;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Parsers.ElasticQueries;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class StackIndex : VersionedIndex<Stack> {
        private readonly ExceptionlessElasticConfiguration _configuration;

        public StackIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "stacks", 1) {
            _configuration = configuration;
        }

        public override ITypeMapping ConfigureIndexMapping(TypeMappingDescriptor<Stack> map) {
            return map
                .Dynamic(false)
                .Properties(p => p
                    .SetupDefaults()
                    .Keyword(f => f.Name(s => s.OrganizationId))
                        .FieldAlias(a => a.Name(Alias.OrganizationId).Path(f => f.OrganizationId))
                    .Keyword(f => f.Name(s => s.ProjectId))
                        .FieldAlias(a => a.Name(Alias.ProjectId).Path(f => f.ProjectId))
                    .Keyword(f => f.Name(s => s.SignatureHash))
                        .FieldAlias(a => a.Name(Alias.SignatureHash).Path(f => f.SignatureHash))
                    .Keyword(f => f.Name(e => e.Type))
                        .FieldAlias(a => a.Name(Alias.Type).Path(f => f.Type))
                    .Date(f => f.Name(s => s.FirstOccurrence))
                        .FieldAlias(a => a.Name(Alias.FirstOccurrence).Path(f => f.FirstOccurrence))
                    .Date(f => f.Name(s => s.LastOccurrence))
                        .FieldAlias(a => a.Name(Alias.LastOccurrence).Path(f => f.LastOccurrence))
                    .Text(f => f.Name(s => s.Title).IncludeInAll().Boost(1.1))
                        .FieldAlias(a => a.Name(Alias.Title).Path(f => f.Title))
                    .Text(f => f.Name(s => s.Description).IncludeInAll())
                        .FieldAlias(a => a.Name(Alias.Description).Path(f => f.Description))
                    .Text(f => f.Name(s => s.Tags).IncludeInAll().Boost(1.2).AddKeywordField())
                        .FieldAlias(a => a.Name(Alias.Tags).Path(f => f.Tags))
                    .Text(f => f.Name(s => s.References).IncludeInAll())
                        .FieldAlias(a => a.Name(Alias.References).Path(f => f.References))
                    .Date(f => f.Name(s => s.DateFixed))
                        .FieldAlias(a => a.Name(Alias.DateFixed).Path(f => f.DateFixed))
                    .Boolean(f => f.Name(Alias.IsFixed))
                    .Keyword(f => f.Name(s => s.FixedInVersion))
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
            string dateFixedFieldName = Configuration.Client.Infer.PropertyName(Infer.Property<Stack>(f => f.DateFixed));
            config.AddVisitor(new StackDateFixedQueryVisitor(dateFixedFieldName));
        }

        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .NumberOfShards(_configuration.Options.NumberOfShards)
                .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
                .Priority(5)));
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