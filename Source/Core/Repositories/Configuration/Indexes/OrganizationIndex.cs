using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class OrganizationIndex : IElasticSearchIndex {
        private const string KEYWORD_LOWERCASE = "keyword_lowercase";

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
            var keywordLowercaseAnalyzer = new CustomAnalyzer { Filter = new List<string> { "lowercase" }, Tokenizer = "keyword" };
            return idx.Analysis(descriptor => descriptor.Analyzers(bases => bases.Add(KEYWORD_LOWERCASE, keywordLowercaseAnalyzer)))
                      .AddMapping<Application>(GetApplicationMap)
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
                    .Object<UsageInfo>(ui => ui.Name(o => o.Usage.First()).RootPath().Properties(ip => ip
                        .Date(fu => fu.Name(i => i.Date).IndexName(Fields.Organization.UsageDate))
                        .Number(fu => fu.Name(i => i.Total).IndexName(Fields.Organization.UsageTotal))
                        .Number(fu => fu.Name(i => i.Blocked).IndexName(Fields.Organization.UsageBlocked))
                        .Number(fu => fu.Name(i => i.Limit).IndexName(Fields.Organization.UsageLimit))
                        .Number(fu => fu.Name(i => i.TooBig).IndexName(Fields.Organization.UsageTooBig))))
                    .Object<UsageInfo>(ui => ui.Name(o => o.OverageHours.First()).RootPath().Properties(ip => ip
                        .Date(fu => fu.Name(i => i.Date).IndexName(Fields.Organization.OverageHoursDate))
                        .Number(fu => fu.Name(i => i.Total).IndexName(Fields.Organization.OverageHoursTotal))
                        .Number(fu => fu.Name(i => i.Blocked).IndexName(Fields.Organization.OverageHoursBlocked))
                        .Number(fu => fu.Name(i => i.Limit).IndexName(Fields.Organization.OverageHoursLimit))
                        .Number(fu => fu.Name(i => i.TooBig).IndexName(Fields.Organization.OverageHoursTooBig))))
                );
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
                    .String(f => f.Name(u => u.EmailAddress).Analyzer(KEYWORD_LOWERCASE))
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

                public const string UsageDate = "usage.date";
                public const string UsageTotal = "usage.total";
                public const string UsageBlocked = "usage.blocked";
                public const string UsageLimit = "usage.limit";
                public const string UsageTooBig = "usage.toobig";

                public const string OverageHoursDate = "overage.date";
                public const string OverageHoursTotal = "overage.total";
                public const string OverageHoursBlocked = "overage.blocked";
                public const string OverageHoursLimit = "overage.limit";
                public const string OverageHoursTooBig = "overage.toobig";
            }

            public class User {
                public const string OAuthAccountProviderUserId = "oauthaccount.provideruserid";
            }
        }
    }
}