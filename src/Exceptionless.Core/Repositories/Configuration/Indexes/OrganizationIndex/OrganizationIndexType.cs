using System;
using System.Linq;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class OrganizationIndexType : IndexTypeBase<Organization> {
        public OrganizationIndexType(OrganizationIndex index) : base(index, "organization") { }

        public override TypeMappingDescriptor<Organization> BuildMapping(TypeMappingDescriptor<Organization> map) {
            return base.BuildMapping(map)
                .Dynamic(false)
                .Properties(p => p
                    .SetupDefaults()
                    .Text(f => f.Name(e => e.Name).AddKeywordField())
                    .Keyword(f => f.Name(u => u.StripeCustomerId))
                    .Boolean(f => f.Name(u => u.HasPremiumFeatures))
                    .Keyword(f => f.Name(u => u.PlanId))
                    .Keyword(f => f.Name(u => u.PlanName).IgnoreAbove(256))
                    .Date(f => f.Name(u => u.SubscribeDate))
                    .Number(f => f.Name(u => u.BillingStatus))
                    .Scalar(f => f.BillingPrice, f => f)
                    .Boolean(f => f.Name(u => u.IsSuspended))
                    .Scalar(f => f.RetentionDays, f => f)
                    .Object<Invite>(f => f.Name(o => o.Invites.First()).Properties(ip => ip
                        .Keyword(fu => fu.Name(i => i.Token))
                        .Text(fu => fu.Name(i => i.EmailAddress).Analyzer(OrganizationIndex.KEYWORD_LOWERCASE_ANALYZER))))
                    .AddUsageMappings());
        }
    }

    internal static class OrganizationIndexTypeExtensions {
        public static PropertiesDescriptor<Organization> AddUsageMappings(this PropertiesDescriptor<Organization> descriptor) {
            return descriptor
                .Object<UsageInfo>(ui => ui.Name(o => o.Usage.First()).Properties(p => p
                    .Date(fu => fu.Name(i => i.Date))
                    .Number(fu => fu.Name(i => i.Total))
                    .Number(fu => fu.Name(i => i.Blocked))
                    .Number(fu => fu.Name(i => i.Limit))
                    .Number(fu => fu.Name(i => i.TooBig))))
                .Object<UsageInfo>(ui => ui.Name(o => o.OverageHours.First()).Properties(p => p
                    .Date(fu => fu.Name(i => i.Date))
                    .Number(fu => fu.Name(i => i.Total))
                    .Number(fu => fu.Name(i => i.Blocked))
                    .Number(fu => fu.Name(i => i.Limit))
                    .Number(fu => fu.Name(i => i.TooBig))));
        }
    }
}