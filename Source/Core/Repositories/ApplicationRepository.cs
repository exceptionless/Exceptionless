using System;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Logging;
using Foundatio.Messaging;

namespace Exceptionless.Core.Repositories {
    public class ApplicationRepository : RepositoryOwnedByOrganization<Application>, IApplicationRepository {
        public ApplicationRepository(ExceptionlessElasticConfiguration configuration, IValidator<Application> validator, ICacheClient cache, IMessagePublisher messagePublisher, ILogger<ApplicationRepository> logger) 
            : base(configuration.Client, validator, cache, messagePublisher, logger) {
            ElasticType = configuration.Organizations.Application;
        }
    }
}
