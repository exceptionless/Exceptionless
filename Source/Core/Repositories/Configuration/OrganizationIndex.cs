using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Nest;
using Token = Exceptionless.Core.Models.Token;

namespace Exceptionless.Core.Repositories.Configuration {
    public class OrganizationIndex : IElasticSearchIndex {
        public int Version { get { return 1; } }

        public const string Alias = "organization";
        public string Name { get { return "organization"; } }

        public string VersionedName {
            get { return String.Concat(Name, "-v", Version); }
        }

        public IDictionary<Type, string> GetIndexTypes() {
            return new Dictionary<Type, string> {
                { typeof(Organization), "organization" },
                { typeof(User), "user" },
                { typeof(Token), "token" },
                { typeof(Application), "application" },
                { typeof(WebHook), "webhook" },
            };
        }

        public CreateIndexDescriptor CreateIndex(CreateIndexDescriptor idx) {
            //idx.AddMapping<Organization>(GetOrganizationMap).AddMapping<User>(GetUserMap);
            return idx;
        }

        //public PutMappingDescriptor<Organization> GetOrganizationMap(PutMappingDescriptor<Organization> map) {
        //    return map
        //        .Index(VersionedName)
        //        .Dynamic()
        //        .TimestampField(ts => ts.Enabled().Path(u => u.UpdatedUtc).IgnoreMissing(false))
        //        .Properties(p => p
        //            .String(f => f.Name(u => u.SubdomainName).Index(FieldIndexOption.NotAnalyzed))
        //            .Object<Invite>(f => f.Name(o => o.Invites.First()).RootPath().Properties(ip => ip
        //                .String(fu => fu.Name(i => i.Token).Index(FieldIndexOption.NotAnalyzed).IndexName(Fields.Organization.InviteToken))))
        //        );
        //}

        //public PutMappingDescriptor<User> GetUserMap(PutMappingDescriptor<User> map) {
        //    return map
        //        .Index(VersionedName)
        //        .Dynamic()
        //        .TimestampField(ts => ts.Enabled().Path(u => u.UpdatedUtc).IgnoreMissing(false))
        //        .Properties(p => p
        //            .String(f => f.Name(u => u.EmailAddress).Index(FieldIndexOption.NotAnalyzed))
        //            .String(f => f.Name(u => u.PasswordResetToken).Index(FieldIndexOption.NotAnalyzed))
        //            .String(f => f.Name(u => u.VerifyEmailAddressToken).Index(FieldIndexOption.NotAnalyzed))
        //            .Object<OAuthAccount>(f => f.Name(o => o.OAuthAccounts.First()).RootPath().Properties(mp => mp
        //                .String(fu => fu.Name(m => m.ProviderUserId).Index(FieldIndexOption.NotAnalyzed).IndexName(Fields.User.OAuthAccountProviderUserId))))
        //            .Object<Membership>(f => f.Name(o => o.Memberships.First()).RootPath().Properties(mp =>
        //                mp.String(fu => fu.Name(m => m.OrganizationId).Index(FieldIndexOption.NotAnalyzed).IndexName(Fields.User.MembershipOrganizationId))))
        //        );
        //}

        public static class Fields {
            public class Organization {
                public const string InviteToken = "invite_token";
            }

            public static class User {
                public const string OAuthAccountProviderUserId = "oauthaccount_provideruserid";
                public const string MembershipOrganizationId = "membership_organizationId";
            }
        }
    }
}
