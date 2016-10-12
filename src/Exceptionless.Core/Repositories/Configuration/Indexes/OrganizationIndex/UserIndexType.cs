using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class UserIndexType : IndexTypeBase<Models.User> {
        private const string KEYWORD_LOWERCASE_ANALYZER = "keyword_lowercase";

        public UserIndexType(OrganizationIndex index) : base(index, "user") { }

        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            return base.Configure(idx)
                .Settings(s => s
                    .Analysis(d => d.Analyzers(b => b.Custom(KEYWORD_LOWERCASE_ANALYZER, c => c.Filters("lowercase").Tokenizer("keyword"))))
                    .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                    .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas));
        }

        public override TypeMappingDescriptor<Models.User> BuildMapping(TypeMappingDescriptor<Models.User> map) {
            return base.BuildMapping(map)
                .Dynamic()
                .Properties(p => p
                    .SetupDefaults()
                    .Date(f => f.Name(e => e.ModifiedUtc))
                    .Keyword(f => f.Name(e => e.OrganizationIds))
                    .Text(f => f.Name(u => u.FullName))
                    .Text(f => f.Name(u => u.EmailAddress).Analyzer(KEYWORD_LOWERCASE_ANALYZER))
                    .Keyword(f => f.Name(u => u.VerifyEmailAddressToken))
                    .Keyword(f => f.Name(u => u.Password))
                    .Keyword(f => f.Name(u => u.PasswordResetToken))
                    .Keyword(f => f.Name(u => u.Salt))
                    .Keyword(f => f.Name(u => u.Roles))
                    .Object<OAuthAccount>(f => f.Name(o => o.OAuthAccounts.First()).Properties(mp => mp
                        .Keyword(fu => fu.Name(m => m.Provider))
                        .Keyword(fu => fu.Name(m => m.ProviderUserId))
                        .Keyword(fu => fu.Name(m => m.Username))))
                );
        }
    }
}