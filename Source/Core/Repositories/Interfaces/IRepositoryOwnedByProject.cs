using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IRepositoryOwnedByProject<T> : IRepository<T>, IElasticsearchReadOnlyRepository<T> where T : class, IOwnedByProject, IIdentity, new() {
        Task<FindResults<T>> GetByProjectIdAsync(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);
        Task RemoveAllByProjectIdsAsync(string[] projectIds);
    }
}