using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface IReadOnlyRepository<T> where T : class, IIdentity, new() {
        void InvalidateCache(string cacheKey);
        void InvalidateCache(T document);
        long Count();
        T GetById(string id, bool useCache = false, TimeSpan? expiresIn = null);
        FindResults<T> GetByIds(ICollection<string> ids, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);
        FindResults<T> GetAll(string sort = null, SortOrder sortOrder = SortOrder.Ascending, PagingOptions paging = null);
        FindResults<T> GetBySearch(string systemFilter, string userFilter = null, string query = null, string sort = null, SortOrder sortOrder = SortOrder.Ascending, PagingOptions paging = null);
        bool Exists(string id);
    }
}