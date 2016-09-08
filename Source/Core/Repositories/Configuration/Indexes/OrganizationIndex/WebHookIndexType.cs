using System;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class WebHookIndexType : IndexTypeBase<WebHook> {
        public WebHookIndexType(OrganizationIndex index) : base(index, "webhook") { }

        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            return base.Configure(idx)
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas);
        }

        public override PutMappingDescriptor<WebHook> BuildMapping(PutMappingDescriptor<WebHook> map) {
            return map
                .Type(Name)
                .Dynamic()
                .Properties(p => p
                    .SetupDefaults()
                    .String(f => f.Name(e => e.OrganizationId).IndexName(Fields.OrganizationId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.ProjectId).IndexName(Fields.ProjectId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Url).IndexName(Fields.Url).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.EventTypes).IndexName(Fields.EventTypes).Index(FieldIndexOption.NotAnalyzed))
                );
        }

        public class Fields {
            public const string CreatedUtc = "created";
            public const string OrganizationId = "organization";
            public const string ProjectId = "project";
            public const string Id = "id";
            public const string Url = "url";
            public const string EventTypes = "types";
        }
    }
}