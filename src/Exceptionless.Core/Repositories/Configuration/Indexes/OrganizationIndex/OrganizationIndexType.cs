using System;
using System.Linq;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
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

        // TODO: Let the query parser know about our analyzed fields for smarter query generation.
        //public bool IsAnalyzedField(string field) {
        //    return field == Fields.Name;
        //}

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
}