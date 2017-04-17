using System;
using System.Linq;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class UserIndexType : IndexTypeBase<Models.User> {
        public UserIndexType(OrganizationIndex index) : base(index, "user") { }

        public override TypeMappingDescriptor<Models.User> BuildMapping(TypeMappingDescriptor<Models.User> map) {
            return base.BuildMapping(map)
                .Dynamic(false)
                .Properties(p => p
                    .SetupDefaults()
                    .Keyword(f => f.Name(e => e.OrganizationIds))
                    .Text(f => f.Name(u => u.FullName).AddKeywordField())
                    .Text(f => f.Name(u => u.EmailAddress).Analyzer(OrganizationIndex.KEYWORD_LOWERCASE_ANALYZER).AddKeywordField())
                    .Keyword(f => f.Name(u => u.VerifyEmailAddressToken))
                    .Keyword(f => f.Name(u => u.PasswordResetToken))
                    .Keyword(f => f.Name(u => u.Roles))
                    .Object<OAuthAccount>(f => f.Name(o => o.OAuthAccounts.First()).Properties(mp => mp
                        .Keyword(fu => fu.Name(m => m.Provider))
                        .Keyword(fu => fu.Name(m => m.ProviderUserId))
                        .Keyword(fu => fu.Name(m => m.Username))))
                );
        }
    }
}