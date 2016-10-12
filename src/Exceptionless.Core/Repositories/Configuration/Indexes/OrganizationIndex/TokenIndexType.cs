using System;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class TokenIndexType : IndexTypeBase<Models.Token> {
        public TokenIndexType(OrganizationIndex index) : base(index, "token") { }

        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            return base.Configure(idx).Settings(s => s
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas));
        }

        public override TypeMappingDescriptor<Models.Token> BuildMapping(TypeMappingDescriptor<Models.Token> map) {
            return base.BuildMapping(map)
                .Dynamic()
                .Properties(p => p
                    .SetupDefaults()
                    .Keyword(f => f.Name(e => e.CreatedBy))
                    .Date(f => f.Name(e => e.ModifiedUtc))
                    .Keyword(f => f.Name(e => e.ApplicationId))
                    .Keyword(f => f.Name(e => e.OrganizationId))
                    .Keyword(f => f.Name(e => e.ProjectId))
                    .Keyword(f => f.Name(e => e.DefaultProjectId))
                    .Keyword(f => f.Name(e => e.UserId))
                    .Keyword(f => f.Name(e => e.Refresh))
                    .Keyword(f => f.Name(e => e.Scopes))
                    .Text(f => f.Name(u => u.Notes).Index(false))
                );
        }
    }
}