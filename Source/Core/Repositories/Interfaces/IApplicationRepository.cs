using System;
using Exceptionless.Core.Models.Admin;

namespace Exceptionless.Core.Repositories {
    public interface IApplicationRepository : IRepositoryOwnedByOrganization<Application> {}
}