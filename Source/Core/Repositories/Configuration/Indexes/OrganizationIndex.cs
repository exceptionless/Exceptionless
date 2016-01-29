using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Foundatio.Elasticsearch.Configuration;
using Foundatio.Elasticsearch.Extensions;
using Nest;
using Token = Exceptionless.Core.Models.Token;

namespace Exceptionless.Core.Repositories.Configuration {
    public class OrganizationIndex : IElasticIndex {
        public int Version => 1;
        public static string Alias => Settings.Current.AppScopePrefix + "organizations";
        public string AliasName => Alias;
        public string VersionedName => String.Concat(AliasName, "-v", Version);

        public IDictionary<Type, IndexType> GetIndexTypes() {
            return new Dictionary<Type, IndexType> {
                { typeof(Application), new IndexType { Name = "application" } },
                //{ typeof(MigrationResult), new IndexType { Name = "migrations"} },
                { typeof(Organization), new IndexType { Name = "organization" } },
                { typeof(Project), new IndexType { Name = "project" } },
                { typeof(Models.Token), new IndexType { Name = "token" } },
                { typeof(User), new IndexType { Name = "user" } },
                { typeof(WebHook), new IndexType { Name = "webhook" } }
            };
        }

        public CreateIndexDescriptor CreateIndex(CreateIndexDescriptor idx) {
            return idx.Settings(s => s.Analysis(a => BuildAnalysisSettings()))
                .Index(VersionedName)
                .Mappings(maps => maps
                    .Map<Application>(GetApplicationMap)
                    .Map<Organization>(GetOrganizationMap)
                    .Map<Project>(GetProjectMap)
                    .Map<Models.Token>(GetTokenMap)
                    .Map<User>(GetUserMap)
                    .Map<WebHook>(GetWebHookMap));
        }

        private const string KEYWORD_LOWERCASE_ANALYZER = "keyword_lowercase";
        private IAnalysis BuildAnalysisSettings() {
            return new Analysis {
                Analyzers = new Analyzers {
                    { KEYWORD_LOWERCASE_ANALYZER, new CustomAnalyzer { Tokenizer = "keyword", Filter = new [] { "lowercase" } } },
                }
            };
        }

        private ITypeMapping GetApplicationMap(TypeMappingDescriptor<Application> map){
            return map
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                     .String(f => f.Name(e => e.Id).IndexName("id").Index(FieldIndexOption.NotAnalyzed))
                     .String(f => f.Name(e => e.OrganizationId).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
             );
        }

        private ITypeMapping GetOrganizationMap(TypeMappingDescriptor<Organization> map){
            return map
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .Date(f => f.Name(e => e.CreatedUtc).IndexName(Fields.Organization.CreatedUtc))
                    .Date(f => f.Name(e => e.ModifiedUtc).IndexName(Fields.Organization.ModifiedUtc))
                    .String(f => f.Name(e => e.Id).IndexName(Fields.Organization.Id).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Name).IndexName(Fields.Organization.Name).Index(FieldIndexOption.Analyzed))
                    .String(f => f.Name(u => u.StripeCustomerId).IndexName(Fields.Organization.StripeCustomerId).Index(FieldIndexOption.NotAnalyzed))
                    .Boolean(f => f.Name(u => u.HasPremiumFeatures).IndexName(Fields.Organization.HasPremiumFeatures))
                    .String(f => f.Name(u => u.PlanId).IndexName(Fields.Organization.PlanId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.PlanName).IndexName(Fields.Organization.PlanName).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.PlanDescription).IndexName(Fields.Organization.PlanDescription).Index(FieldIndexOption.No))
                    .String(f => f.Name(u => u.CardLast4).IndexName(Fields.Organization.CardLast4).Index(FieldIndexOption.NotAnalyzed))
                    .Date(f => f.Name(u => u.SubscribeDate).IndexName(Fields.Organization.SubscribeDate))
                    .Number(f => f.Name(u => u.BillingStatus).IndexName(Fields.Organization.BillingStatus))
                    .String(f => f.Name(u => u.BillingChangedByUserId).IndexName(Fields.Organization.BillingChangedByUserId).Index(FieldIndexOption.NotAnalyzed))
                    .Number(f => f.Name(u => u.BillingPrice).IndexName(Fields.Organization.BillingPrice))
                    .Boolean(f => f.Name(u => u.IsSuspended).IndexName(Fields.Organization.IsSuspended))
                    .String(f => f.Name(u => u.SuspendedByUserId).IndexName(Fields.Organization.SuspendedByUserId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.SuspensionNotes).IndexName(Fields.Organization.SuspensionNotes).Index(FieldIndexOption.NotAnalyzed))
                    .Number(f => f.Name(u => u.RetentionDays).IndexName(Fields.Organization.RetentionDays))
                    .Object<DataDictionary>(f => f.Name(u => u.Data).Dynamic(false))
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

        private ITypeMapping GetProjectMap(TypeMappingDescriptor<Project> map) {
            return map
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .Date(f => f.Name(e => e.CreatedUtc).IndexName(Fields.Project.CreatedUtc))
                    .Date(f => f.Name(e => e.ModifiedUtc).IndexName(Fields.Project.ModifiedUtc))
                    .String(f => f.Name(e => e.Id).IndexName(Fields.Project.Id).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.OrganizationId).IndexName(Fields.Project.OrganizationId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Name).IndexName(Fields.Project.Name).Index(FieldIndexOption.Analyzed))
                    .String(f => f.Name(u => u.PromotedTabs).IndexName(Fields.Project.PromotedTabs).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.CustomContent).IndexName(Fields.Project.CustomContent).Index(FieldIndexOption.No))
                    .Object<ClientConfiguration>(f => f.Name(u => u.Configuration).Dynamic(false))
                    .Object<Dictionary<string, NotificationSettings>>(f => f.Name(u => u.NotificationSettings).Dynamic(false))
                    .Object<DataDictionary>(f => f.Name(u => u.Data).Dynamic(false))
                );
        }

        private ITypeMapping GetTokenMap(TypeMappingDescriptor<Token> map) {
            return map
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .String(f => f.Name(e => e.CreatedBy).IndexName(Fields.Token.CreatedBy).Index(FieldIndexOption.NotAnalyzed))
                    .Date(f => f.Name(e => e.CreatedUtc).IndexName(Fields.Token.CreatedUtc))
                    .Date(f => f.Name(e => e.ModifiedUtc).IndexName(Fields.Token.ModifiedUtc))
                    .String(f => f.Name(e => e.Id).IndexName(Fields.Token.Id).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.ApplicationId).IndexName(Fields.Token.ApplicationId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.OrganizationId).IndexName(Fields.Token.OrganizationId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.ProjectId).IndexName(Fields.Token.ProjectId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.DefaultProjectId).IndexName(Fields.Token.DefaultProjectId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.UserId).IndexName(Fields.Token.UserId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Refresh).IndexName(Fields.Token.Refresh).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Scopes).IndexName(Fields.Token.Scopes).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.Notes).IndexName(Fields.Token.Notes).Index(FieldIndexOption.No))
                );
        }

        public ITypeMapping GetUserMap(TypeMappingDescriptor<User> map) {
            return map
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .Date(f => f.Name(e => e.CreatedUtc).IndexName(Fields.User.CreatedUtc))
                    .Date(f => f.Name(e => e.ModifiedUtc).IndexName(Fields.User.ModifiedUtc))
                    .String(f => f.Name(e => e.Id).IndexName(Fields.User.Id).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.OrganizationIds).IndexName(Fields.User.OrganizationIds).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.FullName).IndexName(Fields.User.FullName))
                    .String(f => f.Name(u => u.EmailAddress).IndexName(Fields.User.EmailAddress).Analyzer(KEYWORD_LOWERCASE_ANALYZER))
                    .String(f => f.Name(u => u.VerifyEmailAddressToken).IndexName(Fields.User.VerifyEmailAddressToken).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.Password).IndexName(Fields.User.Password).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.PasswordResetToken).IndexName(Fields.User.PasswordResetToken).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.Salt).IndexName(Fields.User.Salt).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.Roles).IndexName(Fields.User.Roles).Index(FieldIndexOption.NotAnalyzed))
                    .Object<OAuthAccount>(f => f.Name(o => o.OAuthAccounts.First()).RootPath().Properties(mp => mp
                        .String(fu => fu.Name(m => m.Provider).IndexName(Fields.User.OAuthAccountProvider).Index(FieldIndexOption.NotAnalyzed))
                        .String(fu => fu.Name(m => m.ProviderUserId).IndexName(Fields.User.OAuthAccountProviderUserId).Index(FieldIndexOption.NotAnalyzed))
                        .String(fu => fu.Name(m => m.Username).IndexName(Fields.User.OAuthAccountUsername).Index(FieldIndexOption.NotAnalyzed))))
                );
        }

        private ITypeMapping GetWebHookMap(TypeMappingDescriptor<WebHook> map) {
            return map
                .Dynamic()
                .Properties(p => p
                    .Date(f => f.Name(e => e.CreatedUtc).IndexName(Fields.WebHook.CreatedUtc))
                    .String(f => f.Name(e => e.Id).IndexName(Fields.WebHook.Id).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.OrganizationId).IndexName(Fields.WebHook.OrganizationId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.ProjectId).IndexName(Fields.WebHook.ProjectId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Url).IndexName(Fields.WebHook.Url).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.EventTypes).IndexName(Fields.WebHook.EventTypes).Index(FieldIndexOption.NotAnalyzed))
                );
        }

        public class Fields {
            public class Organization {
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
                public const string SuspensionNotes = "Suspension_notes";

                public const string InviteToken = "invite_token";
                public const string InviteEmail = "invite_email";

                public const string UsageDate = "usage_date";
                public const string UsageTotal = "usage_total";
                public const string UsageBlocked = "usage_blocked";
                public const string UsageLimit = "usage_limit";
                public const string UsageTooBig = "usage_toobig";

                public const string OverageHoursDate = "overage_date";
                public const string OverageHoursTotal = "overage_total";
                public const string OverageHoursBlocked = "overage_blocked";
                public const string OverageHoursLimit = "overage_limit";
                public const string OverageHoursTooBig = "overage_toobig";
            }

            public class Project {
                public const string CreatedUtc = "created";
                public const string ModifiedUtc = "modified";
                public const string OrganizationId = "organization";
                public const string Id = "id";
                public const string Name = "name";
                public const string PromotedTabs = "promoted_tabs";
                public const string CustomContent = "custom_content";
            }

            public class Token {
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

            public class User {
                public const string CreatedUtc = "created";
                public const string ModifiedUtc = "modified";
                public const string OrganizationIds = "organization";
                public const string Id = "id";
                public const string FullName = "name";
                public const string EmailAddress = "email";
                public const string VerifyEmailAddressToken = "verify_email_token";
                public const string Password = "password";
                public const string PasswordResetToken = "password_reset_token";
                public const string Salt = "salt";
                public const string Roles = "roles";

                public const string OAuthAccountUsername = "oauthaccount_username";
                public const string OAuthAccountProvider = "oauthaccount_provider";
                public const string OAuthAccountProviderUserId = "oauthaccount_provider_user_id";
            }

            public class WebHook {
                public const string CreatedUtc = "created";
                public const string OrganizationId = "organization";
                public const string ProjectId = "project";
                public const string Id = "id";
                public const string Url = "url";
                public const string EventTypes = "types";
            }
        }
    }
}
