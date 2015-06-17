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
            return idx.Settings(s => s.Add("analysis", BuildAnalysisSettings()))
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
                .Dynamic(DynamicMappingOption.Ignore)
                .Properties(p => p
                     .String(f => f.Name(e => e.Id).IndexName("id").Index(FieldIndexOption.NotAnalyzed))
                     .String(f => f.Name(e => e.OrganizationId).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
             );
        }

        private PutMappingDescriptor<Organization> GetOrganizationMap(PutMappingDescriptor<Organization> map){
            return map
                .Index(VersionedName)
                .Dynamic(DynamicMappingOption.Ignore)
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                 .String(f => f.Name(e => e.Id).IndexName("id").Index(FieldIndexOption.NotAnalyzed))
                 .Object<Invite>(f => f.Name(o => o.Invites.First()).RootPath().Properties(ip => ip
                        .String(fu => fu.Name(i => i.Token).Index(FieldIndexOption.NotAnalyzed).IndexName(Fields.Organization.InviteToken))
                        .String(fu => fu.Name(i => i.EmailAddress).Index(FieldIndexOption.Analyzed).IndexAnalyzer("email").SearchAnalyzer("simple").IndexName(Fields.Organization.InviteEmail))))
                );

                // TODO: Do we need to add entries for overage hours and usage... We probably want to query by this..
        }

        private PutMappingDescriptor<Project> GetProjectMap(PutMappingDescriptor<Project> map) {
            return map
                .Index(VersionedName)
                .Dynamic(DynamicMappingOption.Ignore)
                .Properties(p => p
                     .String(f => f.Name(e => e.Id).IndexName("id").Index(FieldIndexOption.NotAnalyzed))
                     .String(f => f.Name(e => e.OrganizationId).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
                );


            //protected override void ConfigureClassMap(BsonClassMap<Project> cm) {
            //    base.ConfigureClassMap(cm);
            //    cm.GetMemberMap(c => c.Name).SetElementName(FieldNames.Name);
            //    cm.GetMemberMap(c => c.Configuration).SetElementName(FieldNames.Configuration);
            //    cm.GetMemberMap(c => c.CustomContent).SetElementName(FieldNames.CustomContent).SetIgnoreIfNull(true);
            //    cm.GetMemberMap(c => c.NextSummaryEndOfDayTicks).SetElementName(FieldNames.NextSummaryEndOfDayTicks);

            //    cm.GetMemberMap(c => c.PromotedTabs).SetElementName(FieldNames.PromotedTabs).SetIgnoreIfNull(true).SetShouldSerializeMethod(obj => ((Project)obj).PromotedTabs.Any());
            //    cm.GetMemberMap(c => c.NotificationSettings).SetElementName(FieldNames.NotificationSettings).SetIgnoreIfNull(true).SetShouldSerializeMethod(obj => ((Project)obj).NotificationSettings.Any());
            //}
        }

        private PutMappingDescriptor<Models.Token> GetTokenMap(PutMappingDescriptor<Models.Token> map) {
            return map
                .Index(VersionedName)
                .Dynamic(DynamicMappingOption.Ignore)
                .Properties(p => p
                     .String(f => f.Name(e => e.Id).IndexName("id").Index(FieldIndexOption.NotAnalyzed))
                     .String(f => f.Name(e => e.OrganizationId).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
                     .String(f => f.Name(e => e.ProjectId).IndexName("project").Index(FieldIndexOption.NotAnalyzed))
                );
            
            //protected override void ConfigureClassMap(BsonClassMap<Token> cm) {
            //    cm.AutoMap();
            //    cm.SetIgnoreExtraElements(true);
            //    cm.SetIdMember(cm.GetMemberMap(c => c.Id));
            //    cm.GetMemberMap(c => c.OrganizationId).SetElementName(FieldNames.OrganizationId).SetRepresentation(BsonType.ObjectId).SetIgnoreIfNull(true);
            //    cm.GetMemberMap(c => c.ProjectId).SetElementName(FieldNames.ProjectId).SetRepresentation(BsonType.ObjectId).SetIgnoreIfNull(true);
            //    cm.GetMemberMap(c => c.UserId).SetElementName(FieldNames.UserId).SetRepresentation(BsonType.ObjectId).SetIgnoreIfNull(true);
            //    cm.GetMemberMap(c => c.ApplicationId).SetElementName(FieldNames.ApplicationId).SetRepresentation(BsonType.ObjectId).SetIgnoreIfNull(true);
            //    cm.GetMemberMap(c => c.DefaultProjectId).SetElementName(FieldNames.DefaultProjectId).SetRepresentation(BsonType.ObjectId).SetIgnoreIfNull(true);
            //    cm.GetMemberMap(c => c.Refresh).SetElementName(FieldNames.Refresh).SetIgnoreIfNull(true);
            //    cm.GetMemberMap(c => c.Type).SetElementName(FieldNames.Type);
            //    cm.GetMemberMap(c => c.Scopes).SetElementName(FieldNames.Scopes).SetShouldSerializeMethod(obj => ((Token)obj).Scopes.Any());
            //    cm.GetMemberMap(c => c.ExpiresUtc).SetElementName(FieldNames.ExpiresUtc).SetIgnoreIfNull(true);
            //    cm.GetMemberMap(c => c.Notes).SetElementName(FieldNames.Notes).SetIgnoreIfNull(true);
            //    cm.GetMemberMap(c => c.CreatedUtc).SetElementName(FieldNames.CreatedUtc);
            //    cm.GetMemberMap(c => c.ModifiedUtc).SetElementName(FieldNames.ModifiedUtc).SetIgnoreIfDefault(true);
            //}
        }

        public PutMappingDescriptor<User> GetUserMap(PutMappingDescriptor<User> map) {
            return map
                .Index(VersionedName)
                .Dynamic(DynamicMappingOption.Ignore)
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .String(f => f.Name(u => u.EmailAddress).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.PasswordResetToken).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(u => u.VerifyEmailAddressToken).Index(FieldIndexOption.NotAnalyzed))
                    .Object<OAuthAccount>(f => f.Name(o => o.OAuthAccounts.First()).RootPath().Properties(mp => mp
                        .String(fu => fu.Name(m => m.ProviderUserId).Index(FieldIndexOption.NotAnalyzed).IndexName(Fields.User.OAuthAccountProviderUserId))))
                );


            //protected override void InitializeCollection(MongoDatabase database) {
            //    base.InitializeCollection(database);

            //    _collection.CreateIndex(IndexKeys<User>.Ascending(u => u.OrganizationIds), IndexOptions.SetBackground(true));
            //    _collection.CreateIndex(IndexKeys<User>.Ascending(u => u.EmailAddress), IndexOptions.SetUnique(true).SetBackground(true));
            //    _collection.CreateIndex(IndexKeys.Ascending(FieldNames.OAuthAccounts_Provider, FieldNames.OAuthAccounts_ProviderUserId), IndexOptions.SetUnique(true).SetSparse(true).SetBackground(true));
            //}

            //protected override void ConfigureClassMap(BsonClassMap<User> cm) {
            //    base.ConfigureClassMap(cm);
            //    cm.GetMemberMap(p => p.OrganizationIds).SetSerializationOptions(new ArraySerializationOptions(new RepresentationSerializationOptions(BsonType.ObjectId)));
            //    cm.GetMemberMap(c => c.IsActive).SetIgnoreIfDefault(true);
            //    cm.GetMemberMap(c => c.IsEmailAddressVerified).SetIgnoreIfDefault(true);
            //    cm.GetMemberMap(c => c.Password).SetIgnoreIfNull(true);
            //    cm.GetMemberMap(c => c.PasswordResetToken).SetIgnoreIfNull(true);
            //    cm.GetMemberMap(c => c.PasswordResetTokenExpiration).SetIgnoreIfDefault(true);
            //    cm.GetMemberMap(c => c.Salt).SetIgnoreIfNull(true);
            //    cm.GetMemberMap(c => c.VerifyEmailAddressToken).SetIgnoreIfNull(true);
            //    cm.GetMemberMap(c => c.VerifyEmailAddressTokenExpiration).SetIgnoreIfDefault(true);
            //}
        }

        private PutMappingDescriptor<WebHook> GetWebHookMap(PutMappingDescriptor<WebHook> map) {
            return map
                .Index(VersionedName)
                .Dynamic(DynamicMappingOption.Ignore)
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

        // TODO: Remove all unused settings.
        private object BuildAnalysisSettings() {
            return new {
                filter = new {
                    email = new {
                        type = "pattern_capture",
                        patterns = new[] {
                            @"(\w+)",
                            @"(\p{L}+)",
                            @"(\d+)",
                            @"(.+)@",
                            @"@(.+)"
                        }
                    },
                    version = new {
                        type = "pattern_capture",
                        patterns = new[] {
                            @"^(\d+)\.",
                            @"^(\d+\.\d+)",
                            @"^(\d+\.\d+\.\d+)"
                        }
                    },
                    version_pad1 = new {
                        type = "pattern_replace",
                        pattern = @"(\.|^)(\d{1})(?=\.|$)",
                        replacement = @"$10000$2"
                    },
                    version_pad2 = new {
                        type = "pattern_replace",
                        pattern = @"(\.|^)(\d{2})(?=\.|$)",
                        replacement = @"$1000$2"
                    },
                    version_pad3 = new {
                        type = "pattern_replace",
                        pattern = @"(\.|^)(\d{3})(?=\.|$)",
                        replacement = @"$100$2"
                    },
                    version_pad4 = new {
                        type = "pattern_replace",
                        pattern = @"(\.|^)(\d{4})(?=\.|$)",
                        replacement = @"$10$2"
                    },
                    typename = new {
                        type = "pattern_capture",
                        patterns = new[] {
                            @"\.(\w+)"
                        }
                    }
                },
                analyzer = new {
                    comma_whitespace = new {
                        type = "pattern",
                        pattern = @"[,\s]+"
                    },
                    email = new {
                        type = "custom",
                        tokenizer = "keyword",
                        filter = new[] {
                            "email",
                            "lowercase",
                            "unique"
                        }
                    },
                    version_index = new {
                        type = "custom",
                        tokenizer = "whitespace",
                        filter = new[] {
                            "version_pad1",
                            "version_pad2",
                            "version_pad3",
                            "version_pad4",
                            "version",
                            "lowercase",
                            "unique"
                        }
                    },
                    version_search = new {
                        type = "custom",
                        tokenizer = "whitespace",
                        filter = new[] {
                            "version_pad1",
                            "version_pad2",
                            "version_pad3",
                            "version_pad4",
                            "lowercase"
                        }
                    },
                    whitespace_lower = new {
                        type = "custom",
                        tokenizer = "whitespace",
                        filter = new[] { "lowercase" }
                    },
                    typename = new {
                        type = "custom",
                        tokenizer = "whitespace",
                        filter = new[] {
                            "typename",
                            "lowercase",
                            "unique"
                        }
                    },
                    standardplus = new {
                        type = "custom",
                        tokenizer = "whitespace",
                        filter = new[] {
                            "standard",
                            "typename",
                            "lowercase",
                            "stop",
                            "unique"
                        }
                    }
                }
            };
        }
    }
}