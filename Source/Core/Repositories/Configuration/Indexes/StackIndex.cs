using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;
using Foundatio.Elasticsearch.Configuration;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class StackIndex : IElasticIndex {
        public int Version => 1;
        public static string Alias => Settings.Current.AppScopePrefix + "stacks";
        public string AliasName => Alias;
        public string VersionedName => String.Concat(AliasName, "-v", Version);

        public IDictionary<Type, IndexType> GetIndexTypes() {
            return new Dictionary<Type, IndexType> {
                { typeof(Stack), new IndexType { Name = "stacks" } }
            };
        }

        public virtual CreateIndexDescriptor CreateIndex(CreateIndexDescriptor idx) {
            const string SET_FIXED_SCRIPT = @"ctx._source['fixed'] = !!ctx._source['date_fixed']";

            return idx.Mappings(maps => maps
                .Map<Stack>(map => map
                    .Dynamic(DynamicMapping.Ignore)
                    .Transform(t => t.Add(s => s.Script(SET_FIXED_SCRIPT).Language(ScriptLang.Groovy)))
                    .AllField(a => a.Enabled(false))
                    .Properties(p => p
                        .String(f => f.Name(e => e.Id).IndexName(Fields.Stack.Id).Index(FieldIndexOption.NotAnalyzed).IncludeInAll())
                        .String(f => f.Name(s => s.OrganizationId).IndexName(Fields.Stack.OrganizationId).Index(FieldIndexOption.NotAnalyzed))
                        .String(f => f.Name(s => s.ProjectId).IndexName(Fields.Stack.ProjectId).Index(FieldIndexOption.NotAnalyzed))
                        .String(f => f.Name(s => s.SignatureHash).IndexName(Fields.Stack.SignatureHash).Index(FieldIndexOption.NotAnalyzed))
                        .String(f => f.Name(e => e.Type).IndexName(Fields.Stack.Type).Index(FieldIndexOption.NotAnalyzed))
                        .Date(f => f.Name(s => s.FirstOccurrence).IndexName(Fields.Stack.FirstOccurrence))
                        .Date(f => f.Name(s => s.LastOccurrence).IndexName(Fields.Stack.LastOccurrence))
                        .String(f => f.Name(s => s.Title).IndexName(Fields.Stack.Title).Index(FieldIndexOption.Analyzed).IncludeInAll().Boost(1.1))
                        .String(f => f.Name(s => s.Description).IndexName(Fields.Stack.Description).Index(FieldIndexOption.Analyzed).IncludeInAll())
                        .String(f => f.Name(s => s.Tags).IndexName(Fields.Stack.Tags).Index(FieldIndexOption.Analyzed).IncludeInAll().Boost(1.2))
                        .String(f => f.Name(s => s.References).IndexName(Fields.Stack.References).Index(FieldIndexOption.Analyzed).IncludeInAll())
                        .Date(f => f.Name(s => s.DateFixed).IndexName(Fields.Stack.DateFixed))
                        .Boolean(f => f.Name(Fields.Stack.IsFixed))
                        .Boolean(f => f.Name(s => s.IsHidden).IndexName(Fields.Stack.IsHidden))
                        .Boolean(f => f.Name(s => s.IsRegressed).IndexName(Fields.Stack.IsRegressed))
                        .Boolean(f => f.Name(s => s.OccurrencesAreCritical).IndexName(Fields.Stack.OccurrencesAreCritical))
                        .Number(f => f.Name(s => s.TotalOccurrences).IndexName(Fields.Stack.TotalOccurrences))
                    )));
        }

        public class Fields {
            public class Stack {
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
}
