using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IRepositoryOwnedByProject<T> : ISearchableRepository<T> where T : class, IOwnedByProject, IIdentity, new() {
        Task<FindResults<T>> GetByProjectIdAsync(string projectId, CommandOptionsDescriptor<T> options = null);
        Task<long> RemoveAllByProjectIdAsync(string organizationId, string projectId);
    }
}
