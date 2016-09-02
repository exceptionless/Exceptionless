using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class OrganizationIndex : VersionedIndex {
        public OrganizationIndex(IElasticConfiguration configuration) : base(configuration, Settings.Current.AppScopePrefix + "organizations", 1) {
            AddType(Application = new ApplicationIndexType(this));
            AddType(Organization = new OrganizationIndexType(this));
            AddType(Project = new ProjectIndexType(this));
            AddType(Token = new TokenIndexType(this));
            AddType(User = new UserIndexType(this));
            AddType(WebHook = new WebHookIndexType(this));
        }
        
        public ApplicationIndexType Application { get; }
        public OrganizationIndexType Organization { get; }
        public ProjectIndexType Project { get; }
        public TokenIndexType Token { get; }
        public UserIndexType User { get; }
        public WebHookIndexType WebHook { get; }
    } 

    public class ApplicationIndexType : IndexTypeBase<Application> {
        public ApplicationIndexType(OrganizationIndex index) : base(index, "application") { }

        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            return base.Configure(idx)
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas);
        }

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
    
    public class OrganizationIndexType : IndexTypeBase<Organization> {
        public OrganizationIndexType(OrganizationIndex index) : base(index, "organization") { }

        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            return base.Configure(idx)
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas);
        }

        public override PutMappingDescriptor<Organization> BuildMapping(PutMappingDescriptor<Organization> map) {
            return map
                .Type(Name)
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .SetupDefaults()
                    .Date(f => f.Name(e => e.ModifiedUtc).IndexName(Fields.ModifiedUtc))
                    .String(f => f.Name(e => e.Name).IndexName(Fields.Name).Index(FieldIndexOption.Analyzed))
                    .String(f => f.Name(u => u.StripeCustomerId).IndexName(Fields.StripeCustomerId).Index(FieldIndexOption.NotAnalyzed))
                    .Boolean(f => f.Name(u => u.HasPremiumFeatures).IndexName(Fields.HasPremiumFeatures))
                    .String(f => f.Name(u => u.PlanId).IndexName(Fields.PlanId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.PlanName).IndexName(Fields.PlanName).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.PlanDescription).IndexName(Fields.PlanDescription).Index(FieldIndexOption.No))
                    .String(f => f.Name(u => u.CardLast4).IndexName(Fields.CardLast4).Index(FieldIndexOption.NotAnalyzed))
                    .Date(f => f.Name(u => u.SubscribeDate).IndexName(Fields.SubscribeDate))
                    .Number(f => f.Name(u => u.BillingStatus).IndexName(Fields.BillingStatus))
                    .String(f => f.Name(u => u.BillingChangedByUserId).IndexName(Fields.BillingChangedByUserId).Index(FieldIndexOption.NotAnalyzed))
                    .Number(f => f.Name(u => u.BillingPrice).IndexName(Fields.BillingPrice))
                    .Boolean(f => f.Name(u => u.IsSuspended).IndexName(Fields.IsSuspended))
                    .String(f => f.Name(u => u.SuspendedByUserId).IndexName(Fields.SuspendedByUserId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.SuspensionNotes).IndexName(Fields.SuspensionNotes).Index(FieldIndexOption.NotAnalyzed))
                    .Number(f => f.Name(u => u.RetentionDays).IndexName(Fields.RetentionDays))
                    .Object<DataDictionary>(f => f.Name(u => u.Data).Dynamic(false))
                    .Object<Invite>(f => f.Name(o => o.Invites.First()).Path("just_name").Properties(ip => ip
                        .String(fu => fu.Name(i => i.Token).Index(FieldIndexOption.NotAnalyzed).IndexName(Fields.InviteToken))
                        .String(fu => fu.Name(i => i.EmailAddress).Index(FieldIndexOption.NotAnalyzed).IndexName(Fields.InviteEmail))))
                    .Object<UsageInfo>(ui => ui.Name(o => o.Usage.First()).Path("just_name").Properties(ip => ip
                        .Date(fu => fu.Name(i => i.Date).IndexName(Fields.UsageDate))
                        .Number(fu => fu.Name(i => i.Total).IndexName(Fields.UsageTotal))
                        .Number(fu => fu.Name(i => i.Blocked).IndexName(Fields.UsageBlocked))
                        .Number(fu => fu.Name(i => i.Limit).IndexName(Fields.UsageLimit))
                        .Number(fu => fu.Name(i => i.TooBig).IndexName(Fields.UsageTooBig))))
                    .Object<UsageInfo>(ui => ui.Name(o => o.OverageHours.First()).Path("just_name").Properties(ip => ip
                        .Date(fu => fu.Name(i => i.Date).IndexName(Fields.OverageHoursDate))
                        .Number(fu => fu.Name(i => i.Total).IndexName(Fields.OverageHoursTotal))
                        .Number(fu => fu.Name(i => i.Blocked).IndexName(Fields.OverageHoursBlocked))
                        .Number(fu => fu.Name(i => i.Limit).IndexName(Fields.OverageHoursLimit))
                        .Number(fu => fu.Name(i => i.TooBig).IndexName(Fields.OverageHoursTooBig))))
                );
        }

        public class Fields {
            public const string CreatedUtc = "created";
            public const string ModifiedUtc = "modified";
            public const string Id = "id";
            public const string Name = "name";
            public const string SubscribeDate = "subscribed";

            public const string PlanId = "plan";
            public const string PlanName = "plan_name";
            public const string PlanDescription = "plan_description";
            public const string HasPremiumFeatures = "premium";
            public const string RetentionDays = "retention";
            public const string StripeCustomerId = "stripe";
            public const string BillingPrice = "price";
            public const string BillingStatus = "status";
            public const string BillingChangedByUserId = "billing_changed_by_user_id";
            public const string CardLast4 = "card_last_4";
            public const string IsSuspended = "suspended";
            public const string SuspendedByUserId = "suspended_by_user_id";
            public const string SuspensionNotes = "suspension_notes";

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

    public class ProjectIndexType : IndexTypeBase<Project> {
        public ProjectIndexType(OrganizationIndex index) : base(index, "project") { }

        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            return base.Configure(idx)
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas);
        }

        public override PutMappingDescriptor<Project> BuildMapping(PutMappingDescriptor<Project> map) {
            return map
                .Type(Name)
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .SetupDefaults()
                    .Date(f => f.Name(e => e.ModifiedUtc).IndexName(Fields.ModifiedUtc))
                    .String(f => f.Name(e => e.OrganizationId).IndexName(Fields.OrganizationId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Name).IndexName(Fields.Name).Index(FieldIndexOption.Analyzed))
                    .String(f => f.Name(u => u.PromotedTabs).IndexName(Fields.PromotedTabs).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.CustomContent).IndexName(Fields.CustomContent).Index(FieldIndexOption.No))
                    .Object<ClientConfiguration>(f => f.Name(u => u.Configuration).Dynamic(false))
                    .Object<Dictionary<string, NotificationSettings>>(f => f.Name(u => u.NotificationSettings).Dynamic(false))
                    .Object<DataDictionary>(f => f.Name(u => u.Data).Dynamic(false))
                );
        }

        public class Fields {
            public const string CreatedUtc = "created";
            public const string ModifiedUtc = "modified";
            public const string OrganizationId = "organization";
            public const string Id = "id";
            public const string Name = "name";
            public const string PromotedTabs = "promoted_tabs";
            public const string CustomContent = "custom_content";
        }
    }

    public class TokenIndexType : IndexTypeBase<Models.Token> {
        public TokenIndexType(OrganizationIndex index) : base(index, "token") { }

        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            return base.Configure(idx)
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas);
        }

        public override PutMappingDescriptor<Models.Token> BuildMapping(PutMappingDescriptor<Models.Token> map) {
            return map
                .Type(Name)
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .SetupDefaults()
                    .String(f => f.Name(e => e.CreatedBy).IndexName(Fields.CreatedBy).Index(FieldIndexOption.NotAnalyzed))
                    .Date(f => f.Name(e => e.ModifiedUtc).IndexName(Fields.ModifiedUtc))
                    .String(f => f.Name(e => e.ApplicationId).IndexName(Fields.ApplicationId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.OrganizationId).IndexName(Fields.OrganizationId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.ProjectId).IndexName(Fields.ProjectId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.DefaultProjectId).IndexName(Fields.DefaultProjectId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.UserId).IndexName(Fields.UserId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Refresh).IndexName(Fields.Refresh).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Scopes).IndexName(Fields.Scopes).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.Notes).IndexName(Fields.Notes).Index(FieldIndexOption.No))
                );
        }
        
        public class Fields {
            public const string CreatedBy = "createdby";
            public const string CreatedUtc = "created";
            public const string ModifiedUtc = "modified";
            public const string ApplicationId = "application";
            public const string OrganizationId = "organization";
            public const string ProjectId = "project";
            public const string DefaultProjectId = "default_project";
            public const string Id = "id";
            public const string UserId = "user";
            public const string Refresh = "refresh";
            public const string Scopes = "scopes";
            public const string Notes = "notes";
        }
    }

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

    public class WebHookIndexType : IndexTypeBase<WebHook> {
        public WebHookIndexType(OrganizationIndex index) : base(index, "webhook") { }

        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            return base.Configure(idx)
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas);
        }

        public override PutMappingDescriptor<WebHook> BuildMapping(PutMappingDescriptor<WebHook> map) {
            return map
                .Type(Name)
                .Dynamic()
                .Properties(p => p
                    .SetupDefaults()
                    .String(f => f.Name(e => e.OrganizationId).IndexName(Fields.OrganizationId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.ProjectId).IndexName(Fields.ProjectId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Url).IndexName(Fields.Url).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.EventTypes).IndexName(Fields.EventTypes).Index(FieldIndexOption.NotAnalyzed))
                );
        }

        public class Fields {
            public const string CreatedUtc = "created";
            public const string OrganizationId = "organization";
            public const string ProjectId = "project";
            public const string Id = "id";
            public const string Url = "url";
            public const string EventTypes = "types";
        }
    }
}