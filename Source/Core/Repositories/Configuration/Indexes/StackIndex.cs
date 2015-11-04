using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;
using Foundatio.Elasticsearch.Configuration;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class StackIndex : IElasticsearchIndex {
        public int Version => 1;
        public static string Alias => Settings.Current.AppScopePrefix + "stacks";
        public string AliasName => Alias;
        public string VersionedName => String.Concat(AliasName, "-v", Version);

        public virtual IDictionary<Type, string> GetIndexTypeNames() {
            return new Dictionary<Type, string> {
                { typeof(Stack), "stacks" }
            };
        }

        public virtual CreateIndexDescriptor CreateIndex(CreateIndexDescriptor idx) {
            const string SET_FIXED_SCRIPT = @"ctx._source['fixed'] = !!ctx._source['date_fixed']";

            return idx.AddMapping<Stack>(map => map
                .Dynamic(DynamicMappingOption.Ignore)
                .Transform(t => t.Script(SET_FIXED_SCRIPT).Language(ScriptLang.Groovy))
                .IncludeInAll(false)
                .Properties(p => p
                    .String(f => f.Name(e => e.Id).IndexName("id").Index(FieldIndexOption.NotAnalyzed).IncludeInAll())
                    .String(f => f.Name(s => s.OrganizationId).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(s => s.ProjectId).IndexName("project").Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(s => s.SignatureHash).IndexName("signature").Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Type).IndexName("type").Index(FieldIndexOption.NotAnalyzed))
                    .Date(f => f.Name(s => s.FirstOccurrence).IndexName("first"))
                    .Date(f => f.Name(s => s.LastOccurrence).IndexName("last"))
                    .String(f => f.Name(s => s.Title).IndexName("title").Index(FieldIndexOption.Analyzed).IncludeInAll().Boost(1.1))
                    .String(f => f.Name(s => s.Description).IndexName("description").Index(FieldIndexOption.Analyzed).IncludeInAll())
                    .String(f => f.Name(s => s.Tags).IndexName("tag").Index(FieldIndexOption.Analyzed).IncludeInAll().Boost(1.2))
                    .String(f => f.Name(s => s.References).IndexName("links").Index(FieldIndexOption.Analyzed).IncludeInAll())
                    .Date(f => f.Name(s => s.DateFixed).IndexName("fixedon"))
                    .Boolean(f => f.Name("fixed"))
                    .Boolean(f => f.Name(s => s.IsHidden).IndexName("hidden"))
                    .Boolean(f => f.Name(s => s.IsRegressed).IndexName("regressed"))
                    .Boolean(f => f.Name(s => s.OccurrencesAreCritical).IndexName("critical"))
                    .Number(f => f.Name(s => s.TotalOccurrences).IndexName("occurrences"))
                ));
        }
    }
}