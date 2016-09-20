using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class ProjectIndexType : IndexTypeBase<Project> {
        public ProjectIndexType(OrganizationIndex index) : base(index, "project") { }

        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            return base.Configure(idx)
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas);
        }

        public override PutMappingDescriptor<Project> BuildMapping(PutMappingDescriptor<Project> map) {
            return map
                .Type(Name)
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .SetupDefaults()
                    .Date(f => f.Name(e => e.ModifiedUtc).IndexName(Fields.ModifiedUtc))
                    .String(f => f.Name(e => e.OrganizationId).IndexName(Fields.OrganizationId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Name).IndexName(Fields.Name).Index(FieldIndexOption.Analyzed))
                    .String(f => f.Name(u => u.PromotedTabs).IndexName(Fields.PromotedTabs).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.CustomContent).IndexName(Fields.CustomContent).Index(FieldIndexOption.No))
                    .Object<ClientConfiguration>(f => f.Name(u => u.Configuration).Dynamic(false))
                    .Object<Dictionary<string, NotificationSettings>>(f => f.Name(u => u.NotificationSettings).Dynamic(false))
                    .Object<DataDictionary>(f => f.Name(u => u.Data).Dynamic(false))
                );
        }

        // TODO: Let the query parser know about our analyzed fields for smarter query generation.
        //public bool IsAnalyzedField(string field) {
        //    return field == Fields.Name;
        //}

        public class Fields {
            public const string CreatedUtc = "created";
            public const string ModifiedUtc = "modified";
            public const string OrganizationId = "organization";
            public const string Id = "id";
            public const string Name = "name";
            public const string PromotedTabs = "promoted_tabs";
            public const string CustomContent = "custom_content";
        }
    }
}