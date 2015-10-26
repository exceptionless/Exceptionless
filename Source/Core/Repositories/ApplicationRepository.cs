using System;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class ApplicationRepository : RepositoryOwnedByOrganization<Application>, IApplicationRepository {
        public ApplicationRepository(IElasticClient elasticClient, OrganizationIndex index, IValidator<Application> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) 
            : base(elasticClient, index, validator, cacheClient, messagePublisher) {}
    }
}