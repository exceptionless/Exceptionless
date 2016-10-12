using System;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;
using Exceptionless.Core.Extensions;

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
            return base.Configure(idx).Settings(s => s
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas));
        }

        public override TypeMappingDescriptor<Stack> BuildMapping(TypeMappingDescriptor<Stack> map) {
            const string SET_FIXED_SCRIPT = @"ctx._source['fixed'] = !!ctx._source['date_fixed']";

            return base.BuildMapping(map)
                .Dynamic(DynamicMapping.Ignore)
                //.Transform(t => t.Script(SET_FIXED_SCRIPT).Language(ScriptLang.Groovy)) // TODO: This needs to use an ingest pipeline
                .AllField(a => a.Enabled(false))
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
                    .Text(f => f.Name(s => s.Tags).Alias(Alias.Tags).IncludeInAll().Boost(1.2))
                    .Text(f => f.Name(s => s.References).Alias(Alias.References).IncludeInAll())
                    .Date(f => f.Name(s => s.DateFixed).Alias(Alias.DateFixed))
                    .Boolean(f => f.Name(Alias.IsFixed))
                    .Boolean(f => f.Name(s => s.IsHidden).Alias(Alias.IsHidden))
                    .Boolean(f => f.Name(s => s.IsRegressed).Alias(Alias.IsRegressed))
                    .Boolean(f => f.Name(s => s.OccurrencesAreCritical).Alias(Alias.OccurrencesAreCritical))
                    .Number(f => f.Name(s => s.TotalOccurrences).Alias(Alias.TotalOccurrences))
                );
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
            public const string IsHidden = "hidden";
            public const string IsRegressed = "regressed";
            public const string OccurrencesAreCritical = "critical";
            public const string TotalOccurrences = "occurrences";
        }
    }
}
