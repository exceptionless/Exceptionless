using System;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Parsers.ElasticQueries;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class StackIndexType : IndexTypeBase<Stack> {
        public StackIndexType(StackIndex index) : base(index, "stacks") { }

        public override TypeMappingDescriptor<Stack> BuildMapping(TypeMappingDescriptor<Stack> map) {
            return base.BuildMapping(map)
                .Dynamic(false)
                .IncludeInAll(false)
                .Properties(p => p
                    .SetupDefaults()
                    .Keyword(f => f.Name(s => s.OrganizationId).Alias(Alias.OrganizationId))
                    .Keyword(f => f.Name(s => s.ProjectId).Alias(Alias.ProjectId))
                    .Keyword(f => f.Name(s => s.SignatureHash).Alias(Alias.SignatureHash))
                    .Keyword(f => f.Name(e => e.Type).Alias(Alias.Type))
                    .Date(f => f.Name(s => s.FirstOccurrence).Alias(Alias.FirstOccurrence))
                    .Date(f => f.Name(s => s.LastOccurrence).Alias(Alias.LastOccurrence))
                    .Text(f => f.Name(s => s.Title).Alias(Alias.Title).IncludeInAll().Boost(1.1))
                    .Text(f => f.Name(s => s.Description).Alias(Alias.Description).IncludeInAll())
                    .Text(f => f.Name(s => s.Tags).Alias(Alias.Tags).IncludeInAll().Boost(1.2).AddKeywordField())
                    .Text(f => f.Name(s => s.References).Alias(Alias.References).IncludeInAll())
                    .Date(f => f.Name(s => s.DateFixed).Alias(Alias.DateFixed))
                    .Boolean(f => f.Name(Alias.IsFixed))
                    .Keyword(f => f.Name(s => s.FixedInVersion).Alias(Alias.FixedInVersion))
                    .Boolean(f => f.Name(s => s.IsHidden).Alias(Alias.IsHidden))
                    .Boolean(f => f.Name(s => s.IsRegressed).Alias(Alias.IsRegressed))
                    .Boolean(f => f.Name(s => s.OccurrencesAreCritical).Alias(Alias.OccurrencesAreCritical))
                    .Scalar(f => f.TotalOccurrences, f => f.Alias(Alias.TotalOccurrences))
                );
        }

        protected override void ConfigureQueryParser(ElasticQueryParserConfiguration config) {
            string dateFixedFieldName = Configuration.Client.Infer.PropertyName(Infer.Property<Stack>(f => f.DateFixed));
            config.AddVisitor(new StackDateFixedQueryVisitor(dateFixedFieldName));
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