using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class StackIndex : VersionedIndex {
        public StackIndex(IElasticConfiguration configuration) : base(configuration, Settings.Current.AppScopePrefix + "stacks", 1) {
            AddType(Stack = new StackIndexType(this));
        }

        public StackIndexType Stack { get; }
    }

    public class StackIndexType : IndexTypeBase<Stack> {
        public StackIndexType(StackIndex index) : base(index, "stacks") { }

        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            return base.Configure(idx)
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas);
        }

        public override PutMappingDescriptor<Stack> BuildMapping(PutMappingDescriptor<Stack> map) {
            const string SET_FIXED_SCRIPT = @"ctx._source['fixed'] = !!ctx._source['date_fixed']";

            return map
                .Type(Name)
                .Dynamic(DynamicMappingOption.Ignore)
                .Transform(t => t.Script(SET_FIXED_SCRIPT).Language(ScriptLang.Groovy))
                .IncludeInAll(false)
                .Properties(p => p
                    .SetupDefaults()
                    .String(f => f.Name(s => s.OrganizationId).IndexName(Fields.OrganizationId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(s => s.ProjectId).IndexName(Fields.ProjectId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(s => s.SignatureHash).IndexName(Fields.SignatureHash).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Type).IndexName(Fields.Type).Index(FieldIndexOption.NotAnalyzed))
                    .Date(f => f.Name(s => s.FirstOccurrence).IndexName(Fields.FirstOccurrence))
                    .Date(f => f.Name(s => s.LastOccurrence).IndexName(Fields.LastOccurrence))
                    .String(f => f.Name(s => s.Title).IndexName(Fields.Title).Index(FieldIndexOption.Analyzed).IncludeInAll().Boost(1.1))
                    .String(f => f.Name(s => s.Description).IndexName(Fields.Description).Index(FieldIndexOption.Analyzed).IncludeInAll())
                    .String(f => f.Name(s => s.Tags).IndexName(Fields.Tags).Index(FieldIndexOption.Analyzed).IncludeInAll().Boost(1.2))
                    .String(f => f.Name(s => s.References).IndexName(Fields.References).Index(FieldIndexOption.Analyzed).IncludeInAll())
                    .Date(f => f.Name(s => s.DateFixed).IndexName(Fields.DateFixed))
                    .Boolean(f => f.Name(Fields.IsFixed))
                    .Boolean(f => f.Name(s => s.IsHidden).IndexName(Fields.IsHidden))
                    .Boolean(f => f.Name(s => s.IsRegressed).IndexName(Fields.IsRegressed))
                    .Boolean(f => f.Name(s => s.OccurrencesAreCritical).IndexName(Fields.OccurrencesAreCritical))
                    .Number(f => f.Name(s => s.TotalOccurrences).IndexName(Fields.TotalOccurrences))
                );
        }

        private readonly List<string> _analyzedFields = new List<string> {
            Fields.Title,
            Fields.Description,
            Fields.Tags,
            Fields.References
        };

        public override bool IsAnalyzedField(string field) {
            return _analyzedFields.Contains(field);
        }

        public class Fields {
            public const string OrganizationId = "organization";
            public const string ProjectId = "project";
            public const string Id = "id";
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
            public const string IsHidden = "hidden";
            public const string IsRegressed = "regressed";
            public const string OccurrencesAreCritical = "critical";
            public const string TotalOccurrences = "occurrences";
        }
    }
}
