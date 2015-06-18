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
            return idx.AddMapping<Application>(GetApplicationMap)
                      .AddMapping<Organization>(GetOrganizationMap)
                      .AddMapping<Project>(GetProjectMap)
                      .AddMapping<Models.Token>(GetTokenMap)
                      .AddMapping<User>(GetUserMap)
                      .AddMapping<WebHook>(GetWebHookMap);
        }

        private PutMappingDescriptor<Application> GetApplicationMap(PutMappingDescriptor<Application> map){
            return map
                .Index(VersionedName)
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                     .String(f => f.Name(e => e.Id).IndexName("id").Index(FieldIndexOption.NotAnalyzed))
                     .String(f => f.Name(e => e.OrganizationId).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
             );
        }

        private PutMappingDescriptor<Organization> GetOrganizationMap(PutMappingDescriptor<Organization> map){
            return map
                .Index(VersionedName)
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                 .String(f => f.Name(e => e.Id).IndexName("id").Index(FieldIndexOption.NotAnalyzed))
                 .Object<Invite>(f => f.Name(o => o.Invites.First()).RootPath().Properties(ip => ip
                        .String(fu => fu.Name(i => i.Token).Index(FieldIndexOption.NotAnalyzed).IndexName(Fields.Organization.InviteToken))
                        .String(fu => fu.Name(i => i.EmailAddress).Index(FieldIndexOption.NotAnalyzed).IndexName(Fields.Organization.InviteEmail))))
                );

                // TODO: Do we need to add entries for overage hours and usage... We probably want to query by this..
        }

        private PutMappingDescriptor<Project> GetProjectMap(PutMappingDescriptor<Project> map) {
            return map
                .Index(VersionedName)
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                     .String(f => f.Name(e => e.Id).IndexName("id").Index(FieldIndexOption.NotAnalyzed))
                     .String(f => f.Name(e => e.OrganizationId).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
                );
        }

        private PutMappingDescriptor<Models.Token> GetTokenMap(PutMappingDescriptor<Models.Token> map) {
            return map
                .Index(VersionedName)
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                     .String(f => f.Name(e => e.Id).IndexName("id").Index(FieldIndexOption.NotAnalyzed))
                     .String(f => f.Name(e => e.ApplicationId).IndexName("application").Index(FieldIndexOption.NotAnalyzed))
                     .String(f => f.Name(e => e.OrganizationId).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
                     .String(f => f.Name(e => e.ProjectId).IndexName("project").Index(FieldIndexOption.NotAnalyzed))
                     .String(f => f.Name(e => e.DefaultProjectId).IndexName("default_project").Index(FieldIndexOption.NotAnalyzed))
                     .String(f => f.Name(e => e.UserId).IndexName("user").Index(FieldIndexOption.NotAnalyzed))
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

        private PutMappingDescriptor<WebHook> GetWebHookMap(PutMappingDescriptor<WebHook> map) {
            return map
                .Index(VersionedName)
                .Dynamic()
                .Properties(p => p
                     .String(f => f.Name(e => e.Id).IndexName("id").Index(FieldIndexOption.NotAnalyzed))
                     .String(f => f.Name(e => e.OrganizationId).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
                     .String(f => f.Name(e => e.ProjectId).IndexName("project").Index(FieldIndexOption.NotAnalyzed))
                );
        }

        public static class Fields {
            public class Organization {
                public const string InviteToken = "invite.token";
                public const string InviteEmail = "invite.email";
            }

            public class User {
                public const string OAuthAccountProviderUserId = "oauthaccount.provideruserid";
            }
        }
    }
}