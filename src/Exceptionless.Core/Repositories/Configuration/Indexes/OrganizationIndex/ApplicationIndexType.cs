using System;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class ApplicationIndexType : IndexTypeBase<Application> {
        public ApplicationIndexType(OrganizationIndex index) : base(index, "application") { }

        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            return base.Configure(idx).Settings(s => s
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas));
        }

        public override TypeMappingDescriptor<Application> BuildMapping(TypeMappingDescriptor<Application> map) {
            return base.BuildMapping(map)
                .Dynamic()
                .Properties(p => p
                    .SetupDefaults()
                    .Keyword(f => f.Name(e => e.OrganizationId).IndexName("organization"))
                );
        }
    }
}