using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IRepositoryOwnedByProject<T> : IRepository<T> where T : class, IOwnedByProject, IIdentity, new() {
        Task<FindResults<T>> GetByProjectIdAsync(string projectId, PagingOptions paging = null);
        Task<long> RemoveAllByProjectIdAsync(string organizationId, string projectId);
    }
}
