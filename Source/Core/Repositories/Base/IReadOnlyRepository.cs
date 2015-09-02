using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface IReadOnlyRepository<T> where T : class, IIdentity, new() {
        void InvalidateCache(string cacheKey);
        void InvalidateCache(T document);
        long Count();
        T GetById(string id, bool useCache = false, TimeSpan? expiresIn = null);
        ICollection<T> GetByIds(ICollection<string> ids, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);
        bool Exists(string id);
    }
}