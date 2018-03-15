using System;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class TokenIndexType : IndexTypeBase<Models.Token> {
        public TokenIndexType(OrganizationIndex index) : base(index, "token") { }

        public override TypeMappingDescriptor<Models.Token> BuildMapping(TypeMappingDescriptor<Models.Token> map) {
            return base.BuildMapping(map)
                .Dynamic(false)
                .Properties(p => p
                    .SetupDefaults()
                    .Date(f => f.Name(e => e.ExpiresUtc))
                    .Keyword(f => f.Name(e => e.OrganizationId))
                    .Keyword(f => f.Name(e => e.ProjectId))
                    .Keyword(f => f.Name(e => e.DefaultProjectId))
                    .Keyword(f => f.Name(e => e.UserId))
                    .Keyword(f => f.Name(u => u.CreatedBy))
                    .Keyword(f => f.Name(e => e.Refresh))
                    .Keyword(f => f.Name(e => e.Scopes))
                    .Number(f => f.Name(e => e.Type).Type(NumberType.Byte)));
        }
    }
}