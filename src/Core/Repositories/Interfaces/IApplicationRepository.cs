using System;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface IApplicationRepository : IRepositoryOwnedByOrganization<Application> {}
}