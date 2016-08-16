using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Foundatio.Caching;
using Foundatio.Logging;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class OrganizationIndex : VersionedIndex {
        public OrganizationIndex(IElasticClient client, ICacheClient cache = null, ILoggerFactory loggerFactory = null) 
            : base(client, Settings.Current.AppScopePrefix + "organizations", 1, cache, loggerFactory) {

            Application = new ApplicationIndexType(this);
            AddType(Application);

            Organization = new OrganizationIndexType(this);
            AddType(Organization);

            Project = new ProjectIndexType(this);
            AddType(Project);

            Token = new TokenIndexType(this);
            AddType(Token);

            User = new UserIndexType(this);
            AddType(User);

            WebHook = new WebHookIndexType(this);
            AddType(WebHook);
        }
        
        public ApplicationIndexType Application { get; }
        public OrganizationIndexType Organization { get; }
        public ProjectIndexType Project { get; }
        public TokenIndexType Token { get; }
        public UserIndexType User { get; }
        public WebHookIndexType WebHook { get; }
    } 

    public class ApplicationIndexType : IndexType<Application> {
        public ApplicationIndexType(OrganizationIndex index) : base(index, "application") { }

        public override PutMappingDescriptor<Application> BuildMapping(PutMappingDescriptor<Application> map) {
            return map
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .SetupDefaults()
                    .String(f => f.Name(e => e.OrganizationId).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
             );
        }
    }
    
    public class OrganizationIndexType : IndexType<Organization> {
        public OrganizationIndexType(OrganizationIndex index) : base(index, "organization") { }

        public override PutMappingDescriptor<Organization> BuildMapping(PutMappingDescriptor<Organization> map) {
            return map
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .SetupDefaults()
                    .Date(f => f.Name(e => e.ModifiedUtc).IndexName(Fields.ModifiedUtc))
                    .String(f => f.Name(e => e.Name).IndexName(Fields.Name).Index(FieldIndexOption.Analyzed))
                    .String(f => f.Name(u => u.StripeCustomerId).IndexName("stripe").Index(FieldIndexOption.NotAnalyzed))
                    .Boolean(f => f.Name(u => u.HasPremiumFeatures).IndexName("premium"))
                    .String(f => f.Name(u => u.PlanId).IndexName(Fields.PlanId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.PlanName).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.PlanDescription).Index(FieldIndexOption.No))
                    .String(f => f.Name(u => u.CardLast4).Index(FieldIndexOption.NotAnalyzed))
                    .Date(f => f.Name(u => u.SubscribeDate).IndexName(Fields.SubscribeDate))
                    .Number(f => f.Name(u => u.BillingStatus).IndexName(Fields.BillingStatus))
                    .String(f => f.Name(u => u.BillingChangedByUserId).Index(FieldIndexOption.NotAnalyzed))
                    .Number(f => f.Name(u => u.BillingPrice).IndexName(Fields.BillingPrice))
                    .Boolean(f => f.Name(u => u.IsSuspended).IndexName(Fields.IsSuspended))
                    .String(f => f.Name(u => u.SuspendedByUserId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.SuspensionNotes).Index(FieldIndexOption.NotAnalyzed))
                    .Number(f => f.Name(u => u.RetentionDays).IndexName("retention"))
                    .Object<DataDictionary>(f => f.Name(u => u.Data).Dynamic(false))
                    .Object<Invite>(f => f.Name(o => o.Invites.First()).RootPath().Properties(ip => ip
                        .String(fu => fu.Name(i => i.Token).Index(FieldIndexOption.NotAnalyzed).IndexName(Fields.InviteToken))
                        .String(fu => fu.Name(i => i.EmailAddress).Index(FieldIndexOption.NotAnalyzed).IndexName(Fields.InviteEmail))))
                    .Object<UsageInfo>(ui => ui.Name(o => o.Usage.First()).RootPath().Properties(ip => ip
                        .Date(fu => fu.Name(i => i.Date).IndexName(Fields.UsageDate))
                        .Number(fu => fu.Name(i => i.Total).IndexName(Fields.UsageTotal))
                        .Number(fu => fu.Name(i => i.Blocked).IndexName(Fields.UsageBlocked))
                        .Number(fu => fu.Name(i => i.Limit).IndexName(Fields.UsageLimit))
                        .Number(fu => fu.Name(i => i.TooBig).IndexName(Fields.UsageTooBig))))
                    .Object<UsageInfo>(ui => ui.Name(o => o.OverageHours.First()).RootPath().Properties(ip => ip
                        .Date(fu => fu.Name(i => i.Date).IndexName(Fields.OverageHoursDate))
                        .Number(fu => fu.Name(i => i.Total).IndexName(Fields.OverageHoursTotal))
                        .Number(fu => fu.Name(i => i.Blocked).IndexName(Fields.OverageHoursBlocked))
                        .Number(fu => fu.Name(i => i.Limit).IndexName(Fields.OverageHoursLimit))
                        .Number(fu => fu.Name(i => i.TooBig).IndexName(Fields.OverageHoursTooBig))))
                );
        }

        public class Fields {
            public const string Id = "id";
            public const string CreatedUtc = "created";
            public const string ModifiedUtc = "modified";
            public const string Name = "name";
            public const string SubscribeDate = "subscribed";

            public const string BillingPrice = "price";
            public const string BillingStatus = "status";
            public const string IsSuspended = "suspended";
            public const string PlanId = "plan";

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
    }

    public class ProjectIndexType : IndexType<Project> {
        public ProjectIndexType(OrganizationIndex index) : base(index, "project") { }

        public override PutMappingDescriptor<Project> BuildMapping(PutMappingDescriptor<Project> map) {
            return map
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .SetupDefaults()
                    .Date(f => f.Name(e => e.ModifiedUtc).IndexName(Fields.ModifiedUtc))
                    .String(f => f.Name(e => e.OrganizationId).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Name).IndexName(Fields.Name).Index(FieldIndexOption.Analyzed))
                    .String(f => f.Name(u => u.PromotedTabs).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.CustomContent).Index(FieldIndexOption.No))
                    .Object<ClientConfiguration>(f => f.Name(u => u.Configuration).Dynamic(false))
                    .Object<Dictionary<string, NotificationSettings>>(f => f.Name(u => u.NotificationSettings).Dynamic(false))
                    .Object<DataDictionary>(f => f.Name(u => u.Data).Dynamic(false))
                );
        }

        public class Fields {
            public const string Id = "id";
            public const string Name = "name";
            public const string CreatedUtc = "created";
            public const string ModifiedUtc = "modified";
        }
    }

    public class TokenIndexType : IndexType<Models.Token> {
        public TokenIndexType(OrganizationIndex index) : base(index, "token") { }

        public override PutMappingDescriptor<Models.Token> BuildMapping(PutMappingDescriptor<Models.Token> map) {
            return map
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .SetupDefaults()
                    .Date(f => f.Name(e => e.ModifiedUtc).IndexName(Fields.ModifiedUtc))
                    .String(f => f.Name(e => e.CreatedBy).IndexName("createdby").Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.ApplicationId).IndexName("application").Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.OrganizationId).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.ProjectId).IndexName("project").Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.DefaultProjectId).IndexName("default_project").Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.UserId).IndexName("user").Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Refresh).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Scopes).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.Notes).Index(FieldIndexOption.No))
                );
        }
        
        public class Fields {
            public const string CreatedUtc = "created";
            public const string ModifiedUtc = "modified";
        }
    }

    public class UserIndexType : IndexType<User> {
        private const string KEYWORD_LOWERCASE = "keyword_lowercase";

        public UserIndexType(OrganizationIndex index) : base(index, "user") { }
        
        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            var keywordLowercaseAnalyzer = new CustomAnalyzer { Filter = new List<string> { "lowercase" }, Tokenizer = "keyword" };
            return idx.Analysis(descriptor => descriptor.Analyzers(bases => bases.Add(KEYWORD_LOWERCASE, keywordLowercaseAnalyzer)))
                .AddMapping<User>(BuildMapping);
        }

        public override PutMappingDescriptor<User> BuildMapping(PutMappingDescriptor<User> map) {
            return map
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .SetupDefaults()
                    .Date(f => f.Name(e => e.ModifiedUtc).IndexName(Fields.ModifiedUtc))
                    .String(f => f.Name(e => e.OrganizationIds).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.FullName).IndexName("name"))
                    .String(f => f.Name(u => u.EmailAddress).IndexName(Fields.EmailAddress).Analyzer(KEYWORD_LOWERCASE))
                    .String(f => f.Name(u => u.Password).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.Salt).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.PasswordResetToken).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.VerifyEmailAddressToken).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.Roles).Index(FieldIndexOption.NotAnalyzed))
                    .Object<OAuthAccount>(f => f.Name(o => o.OAuthAccounts.First()).RootPath().Properties(mp => mp
                        .String(fu => fu.Name(m => m.Provider).Index(FieldIndexOption.NotAnalyzed))
                        .String(fu => fu.Name(m => m.ProviderUserId).Index(FieldIndexOption.NotAnalyzed).IndexName(Fields.OAuthAccountProviderUserId))
                        .String(fu => fu.Name(m => m.Username).Index(FieldIndexOption.NotAnalyzed))))
                );
        }
        
        public class Fields {
            public const string CreatedUtc = "created";
            public const string ModifiedUtc = "modified";
            public const string OAuthAccountProviderUserId = "oauthaccount.provideruserid";
            public const string EmailAddress = "email";
        }
    }

    public class WebHookIndexType : IndexType<WebHook> {
        public WebHookIndexType(OrganizationIndex index) : base(index, "webhook") { }

        public override PutMappingDescriptor<WebHook> BuildMapping(PutMappingDescriptor<WebHook> map) {
            return map
                .Dynamic()
                .Properties(p => p
                    .SetupDefaults()
                    .String(f => f.Name(e => e.OrganizationId).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.ProjectId).IndexName("project").Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Url).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.EventTypes).IndexName("types").Index(FieldIndexOption.NotAnalyzed))
                );
        }

        public class Fields {
            public const string CreatedUtc = "created";
        }
    }
}