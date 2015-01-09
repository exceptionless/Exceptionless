using System;
using Exceptionless.Models;

namespace Exceptionless.Core.Repositories {
    public interface IRepositoryOwnedByOrganizationAndProject<T> : IRepositoryOwnedByOrganization<T>, IRepositoryOwnedByProject<T> where T : class, IOwnedByOrganizationAndProjectWithIdentity, new() {}
}