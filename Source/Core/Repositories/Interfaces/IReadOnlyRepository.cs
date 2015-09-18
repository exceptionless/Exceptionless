using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface IReadOnlyRepository<T> where T : class, IIdentity, new() {
        Task InvalidateCacheAsync(ICollection<T> documents);
        Task<long> CountAsync();
        Task<T> GetByIdAsync(string id, bool useCache = false, TimeSpan? expiresIn = null);
        Task<FindResults<T>> GetByIdsAsync(ICollection<string> ids, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);
        Task<FindResults<T>> GetAllAsync(string sort = null, SortOrder sortOrder = SortOrder.Ascending, PagingOptions paging = null);
        Task<FindResults<T>> GetBySearchAsync(string systemFilter, string userFilter = null, string query = null, string sort = null, SortOrder sortOrder = SortOrder.Ascending, PagingOptions paging = null);
        Task<bool> ExistsAsync(string id);
    }
}