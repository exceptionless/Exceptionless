using System;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Logging;

namespace Exceptionless.Core.Repositories {
    public class ApplicationRepository : RepositoryOwnedByOrganization<Application>, IApplicationRepository {
        public ApplicationRepository(ElasticRepositoryContext<Application> context, OrganizationIndex index, ILoggerFactory loggerFactory = null) : base(context, index, loggerFactory) { }
    }
}
