using System;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class WebHookIndexType : IndexTypeBase<WebHook> {
        public WebHookIndexType(OrganizationIndex index) : base(index, "webhook") { }

        public override TypeMappingDescriptor<WebHook> BuildMapping(TypeMappingDescriptor<WebHook> map) {
            return base.BuildMapping(map)
                .Dynamic(false)
                .Properties(p => p
                    .SetupDefaults()
                    .Keyword(f => f.Name(e => e.OrganizationId))
                    .Keyword(f => f.Name(e => e.ProjectId))
                    .Keyword(f => f.Name(e => e.Url))
                    .Keyword(f => f.Name(e => e.EventTypes))
                );
        }
    }
}