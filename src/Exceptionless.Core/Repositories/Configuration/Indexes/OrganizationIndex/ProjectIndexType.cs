using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class ProjectIndexType : IndexTypeBase<Project> {
        public ProjectIndexType(OrganizationIndex index) : base(index, "project") { }

        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            return base.Configure(idx).Settings(s => s
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas));
        }

        public override TypeMappingDescriptor<Project> BuildMapping(TypeMappingDescriptor<Project> map) {
            return base.BuildMapping(map)
                .Dynamic()
                .Properties(p => p
                    .SetupDefaults()
                    .Date(f => f.Name(e => e.ModifiedUtc))
                    .Keyword(f => f.Name(e => e.OrganizationId))
                    .Text(f => f.Name(e => e.Name))
                    .Keyword(f => f.Name(u => u.PromotedTabs))
                    .Keyword(f => f.Name(u => u.CustomContent).Index(false))
                    .Object<ClientConfiguration>(f => f.Name(u => u.Configuration).Dynamic(false))
                    .Object<Dictionary<string, NotificationSettings>>(f => f.Name(u => u.NotificationSettings).Dynamic(false))
                    .Object<DataDictionary>(f => f.Name(u => u.Data).Dynamic(false))
                );
        }
    }
}