using System;
using Exceptionless.Core.Models;

namespace Exceptionless.EventMigration.Repositories {
    public interface IApplicationRepository : IRepositoryOwnedByOrganization<Application> {}
}