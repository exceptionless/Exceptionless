using System;
using Exceptionless.Models.Admin;

namespace Exceptionless.Core.Repositories {
    public interface IApplicationRepository : IRepositoryOwnedByOrganization<Application> {}
}