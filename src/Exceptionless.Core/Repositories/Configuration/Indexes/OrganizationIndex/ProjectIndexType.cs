using System;
using System.Linq;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class ProjectIndexType : IndexTypeBase<Project> {
        public ProjectIndexType(OrganizationIndex index) : base(index, "project") { }

        public override TypeMappingDescriptor<Project> BuildMapping(TypeMappingDescriptor<Project> map) {
            return base.BuildMapping(map)
                .Dynamic(false)
                .Properties(p => p
                    .SetupDefaults()
                    .Keyword(f => f.Name(e => e.OrganizationId))
                    .Text(f => f.Name(e => e.Name).AddKeywordField())
                    .Scalar(f => f.NextSummaryEndOfDayTicks, f => f)
                    .AddUsageMappings()
                );
        }
    }

    internal static class ProjectIndexTypeExtensions {
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