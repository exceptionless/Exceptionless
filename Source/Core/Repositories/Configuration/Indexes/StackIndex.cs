using System;
using Exceptionless.Core.Models;
using Foundatio.Caching;
using Foundatio.Logging;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class StackIndex : VersionedIndex {
        public StackIndex(IElasticClient client, ICacheClient cache = null, ILoggerFactory loggerFactory = null) 
            : base(client, Settings.Current.AppScopePrefix + "stacks", 1, cache, loggerFactory) {
            Stack = new StackIndexType(this);
            AddType(Stack);
        }

        public StackIndexType Stack { get; }
    }

    public class StackIndexType : IndexType<Stack> {
        public StackIndexType(StackIndex index) : base(index, "stack") {}

        public override PutMappingDescriptor<Stack> BuildMapping(PutMappingDescriptor<Stack> map) {
            const string SET_FIXED_SCRIPT = @"ctx._source['fixed'] = !!ctx._source['date_fixed']";

            return map
                .Dynamic(DynamicMappingOption.Ignore)
                .Transform(t => t.Script(SET_FIXED_SCRIPT).Language(ScriptLang.Groovy))
                .IncludeInAll(false)
                .Properties(p => p
                    .SetupDefaults()
                    .String(f => f.Name(s => s.OrganizationId).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(s => s.ProjectId).IndexName("project").Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(s => s.SignatureHash).IndexName("signature").Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Type).IndexName("type").Index(FieldIndexOption.NotAnalyzed))
                    .Date(f => f.Name(s => s.FirstOccurrence).IndexName(Fields.FirstOccurrence))
                    .Date(f => f.Name(s => s.LastOccurrence).IndexName(Fields.LastOccurrence))
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
                );
        }

        public class Fields {
            public const string FirstOccurrence = "first";
            public const string LastOccurrence = "last";
        }
    }
}
