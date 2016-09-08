using System;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class TokenIndexType : IndexTypeBase<Models.Token> {
        public TokenIndexType(OrganizationIndex index) : base(index, "token") { }

        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            return base.Configure(idx)
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas);
        }

        public override PutMappingDescriptor<Models.Token> BuildMapping(PutMappingDescriptor<Models.Token> map) {
            return map
                .Type(Name)
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .SetupDefaults()
                    .String(f => f.Name(e => e.CreatedBy).IndexName(Fields.CreatedBy).Index(FieldIndexOption.NotAnalyzed))
                    .Date(f => f.Name(e => e.ModifiedUtc).IndexName(Fields.ModifiedUtc))
                    .String(f => f.Name(e => e.ApplicationId).IndexName(Fields.ApplicationId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.OrganizationId).IndexName(Fields.OrganizationId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.ProjectId).IndexName(Fields.ProjectId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.DefaultProjectId).IndexName(Fields.DefaultProjectId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.UserId).IndexName(Fields.UserId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Refresh).IndexName(Fields.Refresh).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Scopes).IndexName(Fields.Scopes).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.Notes).IndexName(Fields.Notes).Index(FieldIndexOption.No))
                );
        }
        
        public class Fields {
            public const string CreatedBy = "createdby";
            public const string CreatedUtc = "created";
            public const string ModifiedUtc = "modified";
            public const string ApplicationId = "application";
            public const string OrganizationId = "organization";
            public const string ProjectId = "project";
            public const string DefaultProjectId = "default_project";
            public const string Id = "id";
            public const string UserId = "user";
            public const string Refresh = "refresh";
            public const string Scopes = "scopes";
            public const string Notes = "notes";
        }
    }
}