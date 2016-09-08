using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class UserIndexType : IndexTypeBase<User> {
        private const string KEYWORD_LOWERCASE_ANALYZER = "keyword_lowercase";

        public UserIndexType(OrganizationIndex index) : base(index, "user") { }
        
        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            var keywordLowercaseAnalyzer = new CustomAnalyzer { Filter = new List<string> { "lowercase" }, Tokenizer = "keyword" };
            return idx
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas)
                .Analysis(descriptor => descriptor.Analyzers(bases => bases.Add(KEYWORD_LOWERCASE_ANALYZER, keywordLowercaseAnalyzer)))
                .AddMapping<User>(BuildMapping);
        }

        public override PutMappingDescriptor<User> BuildMapping(PutMappingDescriptor<User> map) {
            return map
                .Type(Name)
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .SetupDefaults()
                    .Date(f => f.Name(e => e.ModifiedUtc).IndexName(Fields.ModifiedUtc))
                    .String(f => f.Name(e => e.OrganizationIds).IndexName(Fields.OrganizationIds).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.FullName).IndexName(Fields.FullName))
                    .String(f => f.Name(u => u.EmailAddress).IndexName(Fields.EmailAddress).Analyzer(KEYWORD_LOWERCASE_ANALYZER))
                    .String(f => f.Name(u => u.VerifyEmailAddressToken).IndexName(Fields.VerifyEmailAddressToken).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.Password).IndexName(Fields.Password).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.PasswordResetToken).IndexName(Fields.PasswordResetToken).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.Salt).IndexName(Fields.Salt).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.Roles).IndexName(Fields.Roles).Index(FieldIndexOption.NotAnalyzed))
                    .Object<OAuthAccount>(f => f.Name(o => o.OAuthAccounts.First()).Path("just_name").Properties(mp => mp
                        .String(fu => fu.Name(m => m.Provider).IndexName(Fields.OAuthAccountProvider).Index(FieldIndexOption.NotAnalyzed))
                        .String(fu => fu.Name(m => m.ProviderUserId).IndexName(Fields.OAuthAccountProviderUserId).Index(FieldIndexOption.NotAnalyzed))
                        .String(fu => fu.Name(m => m.Username).IndexName(Fields.OAuthAccountUsername).Index(FieldIndexOption.NotAnalyzed))))
                );
        }
        
        public class Fields {
            public const string CreatedUtc = "created";
            public const string ModifiedUtc = "modified";
            public const string OrganizationIds = "organization";
            public const string Id = "id";
            public const string FullName = "name";
            public const string EmailAddress = "email";
            public const string VerifyEmailAddressToken = "verify_email_address_token";
            public const string Password = "password";
            public const string PasswordResetToken = "password_reset_token";
            public const string Salt = "salt";
            public const string Roles = "roles";

            public const string OAuthAccountUsername = "oauthaccount.username";
            public const string OAuthAccountProvider = "oauthaccount.provider";
            public const string OAuthAccountProviderUserId = "oauthaccount.provideruserid";
        }
    }
}