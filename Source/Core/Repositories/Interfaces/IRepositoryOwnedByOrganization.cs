using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IRepositoryOwnedByOrganization<T> : IRepository<T>, IElasticsearchReadOnlyRepository<T> where T : class, IOwnedByOrganization, IIdentity, new() {
        Task<long> CountByOrganizationIdAsync(string organizationId);
        Task<FindResults<T>> GetByOrganizationIdAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);
        Task<FindResults<T>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);
        Task RemoveAllByOrganizationIdsAsync(string[] organizationIds);
    }
}