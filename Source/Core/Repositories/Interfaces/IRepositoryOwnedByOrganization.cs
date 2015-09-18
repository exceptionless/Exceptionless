using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface IRepositoryOwnedByOrganization<T> : IRepository<T> where T : class, IOwnedByOrganization, IIdentity, new() {
        Task<long> CountByOrganizationIdAsync(string organizationId);
        Task<FindResults<T>> GetByOrganizationIdAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);
        Task<FindResults<T>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);
        Task RemoveAllByOrganizationIdsAsync(string[] organizationIds);
    }
}