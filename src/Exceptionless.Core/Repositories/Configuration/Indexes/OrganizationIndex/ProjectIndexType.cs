using System;
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
                    .Text(f => f.Name(e => e.Name))
                    .Number(f => f.Name(e => e.NextSummaryEndOfDayTicks))
                );
        }
    }
}