using System;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Elasticsearch.Repositories;

namespace Exceptionless.Core.Repositories {
    public class ApplicationRepository : RepositoryOwnedByOrganization<Application>, IApplicationRepository {
        public ApplicationRepository(RepositoryContext<Application> context, OrganizationIndex index) : base(context, index) { }
    }
}