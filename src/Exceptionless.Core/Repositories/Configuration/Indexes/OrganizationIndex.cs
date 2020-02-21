using System.Linq;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class OrganizationIndex : VersionedIndex<Organization> {
        private const string KEYWORD_LOWERCASE_ANALYZER = "keyword_lowercase";
        private readonly ExceptionlessElasticConfiguration _configuration;

        public OrganizationIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "organizations", 1) {
            _configuration = configuration;
        }

        public override TypeMappingDescriptor<Organization> ConfigureIndexMapping(TypeMappingDescriptor<Organization> map) {
            return map
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
                        .Text(fu => fu.Name(i => i.EmailAddress).Analyzer(KEYWORD_LOWERCASE_ANALYZER))))
                    .AddUsageMappings());
        }

        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .Analysis(d => d.Analyzers(b => b.Custom(KEYWORD_LOWERCASE_ANALYZER, c => c.Filters("lowercase").Tokenizer("keyword"))))
                .NumberOfShards(_configuration.Options.NumberOfShards)
                .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
                .Priority(10)));
        }
    }

    internal static class OrganizationIndexExtensions {
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