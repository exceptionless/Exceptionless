using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IRepositoryOwnedByStack<T> : IRepository<T> where T : class, IOwnedByStack, IIdentity, new() {
        Task<IFindResults<T>> GetByStackIdAsync(string stackId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);
        Task RemoveAllByStackIdsAsync(string[] stackIds);
    }

    public interface IRepositoryOwnedByProjectAndStack<T> : IRepositoryOwnedByProject<T>, IRepositoryOwnedByStack<T> where T : class, IOwnedByProject, IIdentity, IOwnedByStack, new() { }

    public interface IRepositoryOwnedByOrganizationAndProjectAndStack<T> : IRepositoryOwnedByOrganization<T>, IRepositoryOwnedByProject<T>, IRepositoryOwnedByStack<T> where T : class, IOwnedByOrganization, IOwnedByProject, IIdentity, IOwnedByStack, new() { }
}
