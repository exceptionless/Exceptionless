using System;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;

namespace Exceptionless.Core.Repositories {
    public class ApplicationRepository : RepositoryOwnedByOrganization<Application>, IApplicationRepository {
        public ApplicationRepository(ExceptionlessElasticConfiguration configuration, IValidator<Application> validator) 
            : base(configuration.Organizations.Application, validator) {}
    }
}
