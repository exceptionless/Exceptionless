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
            return base.Configure(idx).Settings(s => s
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas));
        }

        public override TypeMappingDescriptor<Organization> BuildMapping(TypeMappingDescriptor<Organization> map) {
            return base.BuildMapping(map)
                .Dynamic()
                .Properties(p => p
                    .SetupDefaults()
                    .Date(f => f.Name(e => e.ModifiedUtc))
                    .Text(f => f.Name(e => e.Name))
                    .Keyword(f => f.Name(u => u.StripeCustomerId))
                    .Boolean(f => f.Name(u => u.HasPremiumFeatures))
                    .Keyword(f => f.Name(u => u.PlanId))
                    .Keyword(f => f.Name(u => u.PlanName))
                    .Text(f => f.Name(u => u.PlanDescription).Index(false))
                    .Keyword(f => f.Name(u => u.CardLast4))
                    .Date(f => f.Name(u => u.SubscribeDate))
                    .Number(f => f.Name(u => u.BillingStatus))
                    .Keyword(f => f.Name(u => u.BillingChangedByUserId))
                    .Number(f => f.Name(u => u.BillingPrice))
                    .Boolean(f => f.Name(u => u.IsSuspended))
                    .Keyword(f => f.Name(u => u.SuspendedByUserId))
                    .Keyword(f => f.Name(u => u.SuspensionNotes))
                    .Number(f => f.Name(u => u.RetentionDays))
                    .Object<DataDictionary>(f => f.Name(u => u.Data).Dynamic(false))
                    .Object<Invite>(f => f.Name(o => o.Invites.First()).Properties(ip => ip
                        .Keyword(fu => fu.Name(i => i.Token))
                        .Keyword(fu => fu.Name(i => i.EmailAddress))))
                    .Object<UsageInfo>(ui => ui.Name(o => o.Usage.First()).Properties(ip => ip
                        .Date(fu => fu.Name(i => i.Date))
                        .Number(fu => fu.Name(i => i.Total))
                        .Number(fu => fu.Name(i => i.Blocked))
                        .Number(fu => fu.Name(i => i.Limit))
                        .Number(fu => fu.Name(i => i.TooBig))))
                    .Object<UsageInfo>(ui => ui.Name(o => o.OverageHours.First()).Properties(ip => ip
                        .Date(fu => fu.Name(i => i.Date))
                        .Number(fu => fu.Name(i => i.Total))
                        .Number(fu => fu.Name(i => i.Blocked))
                        .Number(fu => fu.Name(i => i.Limit))
                        .Number(fu => fu.Name(i => i.TooBig))))
                );
        }
    }
}