using System;
using System.Collections.Generic;
using Foundatio.Repositories.Elasticsearch.Queries;

namespace Exceptionless.Core.Repositories.Queries {
    public class ExceptionlessQuery : ElasticQuery, IOrganizationIdQuery, IProjectIdQuery, IStackIdQuery {
        public ExceptionlessQuery() {
            OrganizationIds = new List<string>();
            ProjectIds = new List<string>();
            StackIds = new List<string>();
        }

        public List<string> OrganizationIds { get; }
        public List<string> ProjectIds { get; }
        public List<string> StackIds { get; }
    }
}
