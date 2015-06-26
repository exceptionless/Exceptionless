using System;
using Exceptionless.Core.Models;

namespace Exceptionless.EventMigration.Repositories {
    public interface IRepositoryOwnedByOrganizationAndProject<T> : IRepositoryOwnedByOrganization<T>, IRepositoryOwnedByProject<T> where T : class, IOwnedByOrganizationAndProjectWithIdentity, new() {}
}