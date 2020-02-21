using System.Linq;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class ProjectIndex : VersionedIndex<Project> {
        internal const string KEYWORD_LOWERCASE_ANALYZER = "keyword_lowercase";
        private readonly ExceptionlessElasticConfiguration _configuration;

        public ProjectIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "projects", 1) {
            _configuration = configuration;
        }


        public override TypeMappingDescriptor<Project> ConfigureIndexMapping(TypeMappingDescriptor<Project> map) {
            return map
                .Dynamic(false)
                .Properties(p => p
                    .SetupDefaults()
                    .Keyword(f => f.Name(e => e.OrganizationId))
                    .Text(f => f.Name(e => e.Name).AddKeywordField())
                    .Scalar(f => f.NextSummaryEndOfDayTicks, f => f)
                    .AddUsageMappings()
                );
        }

        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .Analysis(d => d.Analyzers(b => b.Custom(KEYWORD_LOWERCASE_ANALYZER, c => c.Filters("lowercase").Tokenizer("keyword"))))
                .NumberOfShards(_configuration.Options.NumberOfShards)
                .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
                .Priority(10)));
        }
    }

    internal static class ProjectIndexExtensions {
        public static PropertiesDescriptor<Project> AddUsageMappings(this PropertiesDescriptor<Project> descriptor) {
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