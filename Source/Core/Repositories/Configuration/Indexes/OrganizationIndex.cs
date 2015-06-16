using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class OrganizationIndex : IElasticSearchIndex {
        public string Name { get { return "organizations"; } }

        public int Version { get { return 1; } }

        public string VersionedName {
            get { return String.Concat(Name, "-v", Version); }
        }

        public virtual IDictionary<Type, string> GetIndexTypeNames() {
            return new Dictionary<Type, string> {
                { typeof(Application), "application" },
                { typeof(Organization), "organization" },
                { typeof(Project), "project" },
                { typeof(Models.Token), "token" },
                { typeof(User), "user" },
                { typeof(WebHook), "webhook" },
            };
        }

        public CreateIndexDescriptor CreateIndex(CreateIndexDescriptor idx) {
            return idx.AddMapping<Organization>(GetOrganizationMap);
        }

        private PutMappingDescriptor<Organization> GetOrganizationMap(PutMappingDescriptor<Organization> map){
            return map
                .Index(VersionedName)
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .Object<Invite>(f => f.Name(o => o.Invites.First()).RootPath().Properties(ip => ip
                        .String(fu => fu.Name(i => i.Token).Index(FieldIndexOption.NotAnalyzed).IndexName(Fields.Organization.InviteToken))))
                );
        }

        public PutMappingDescriptor<User> GetUserMap(PutMappingDescriptor<User> map) {
            return map
                .Index(VersionedName)
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .String(f => f.Name(u => u.EmailAddress).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.PasswordResetToken).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.VerifyEmailAddressToken).Index(FieldIndexOption.NotAnalyzed))
                    .Object<OAuthAccount>(f => f.Name(o => o.OAuthAccounts.First()).RootPath().Properties(mp => mp
                        .String(fu => fu.Name(m => m.ProviderUserId).Index(FieldIndexOption.NotAnalyzed).IndexName(Fields.User.OAuthAccountProviderUserId))))
                );
        }

        public static class Fields {
            public class Organization {
                public const string InviteToken = "invite_token";
            }
            public static class User {
                public const string OAuthAccountProviderUserId = "oauthaccount_provideruserid";
            }
        }
    }
}