using System;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class ApplicationIndexType : IndexTypeBase<Application> {
        public ApplicationIndexType(OrganizationIndex index) : base(index, "application") { }

        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            return base.Configure(idx)
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas);
        }

        public override PutMappingDescriptor<Application> BuildMapping(PutMappingDescriptor<Application> map) {
            return map
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .SetupDefaults()
                    .String(f => f.Name(e => e.OrganizationId).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
                );
        }
    }
}