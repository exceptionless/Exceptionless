using System;
using System.Linq;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class UserIndex : VersionedIndex<User> {
        private readonly ExceptionlessElasticConfiguration _configuration;

        public UserIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "users", 1) {
            _configuration = configuration;
        }

        public override ITypeMapping ConfigureIndexMapping(TypeMappingDescriptor<User> map) {
            return map
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

        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .NumberOfShards(_configuration.Options.NumberOfShards)
                .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
                .Priority(5)));
        }
    }
}