#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using System.Linq.Expressions;
using MongoDB.Driver;

namespace Exceptionless.Core {
    /// <summary>
    /// IReadOnlyRepository definition.
    /// </summary>
    /// <typeparam name="T">The type contained in the repository.</typeparam>
    public interface IReadOnlyRepository<T> where T : class, new() {
        /// <summary>
        /// Invalidate the cache entry with the specified key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        void InvalidateCache(string cacheKey);

        /// <summary>
        /// Invalidate the cache entries for the entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        void InvalidateCache(T entity);

        /// <summary>
        /// Returns a single T by the given criteria.
        /// </summary>
        /// <param name="criteria">The expression.</param>
        /// <returns>A single T matching the criteria.</returns>
        T FirstOrDefault(Expression<Func<T, bool>> criteria);

        /// <summary>
        /// Returns a single T by the given criteria using a cache.
        /// </summary>
        /// <param name="criteria">The expression.</param>
        /// <param name="cacheKey">The key used to cache the result.</param>
        /// <param name="expiresIn">When the cache should expire relative to now.</param>
        /// <param name="expiresAt">When the cache should expire.</param>
        /// <returns>A single T matching the criteria.</returns>
        T FirstOrDefaultCached(Expression<Func<T, bool>> criteria, string cacheKey, TimeSpan? expiresIn = null, DateTime? expiresAt = null);

        /// <summary>
        /// Returns a IQueryable of T by the given criteria.
        /// </summary>
        /// <param name="criteria">The expression.</param>
        /// <returns>All entities matching the criteria.</returns>
        IQueryable<T> Where(Expression<Func<T, bool>> criteria);

        /// <summary>
        /// Returns a IQueryable of T by the given criteria using a cache.
        /// </summary>
        /// <param name="criteria">The expression.</param>
        /// <param name="cacheKey">The key used to cache the result.</param>
        /// <param name="expiresIn">When the cache should expire relative to now.</param>
        /// <param name="expiresAt">When the cache should expire.</param>
        /// <returns>All entities matching the criteria.</returns>
        IQueryable<T> WhereCached(Expression<Func<T, bool>> criteria, string cacheKey, TimeSpan? expiresIn = null, DateTime? expiresAt = null);

        /// <summary>
        /// Returns a IQueryable of T by the given query.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>All entities matching the query.</returns>
        IQueryable<T> Where(IMongoQuery query);

        /// <summary>
        /// Returns a IQueryable of T by the given query using a cache.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="cacheKey">The key used to cache the result.</param>
        /// <param name="expiresIn">When the cache should expire relative to now.</param>
        /// <param name="expiresAt">When the cache should expire.</param>
        /// <returns>All entities matching the query.</returns>
        IQueryable<T> WhereCached(IMongoQuery query, string cacheKey, TimeSpan? expiresIn = null, DateTime? expiresAt = null);

        /// <summary>
        /// Returns All the records of T.
        /// </summary>
        /// <returns>IQueryable of T.</returns>
        IQueryable<T> All();

        /// <summary>
        /// Counts the total entities in the repository.
        /// </summary>
        /// <returns>Count of entities in the repository.</returns>
        long Count();

        /// <summary>
        /// Counts the total entities in the repository by the given query.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>A count of all entities matching the query.</returns>
        long Count(IMongoQuery query);

        /// <summary>
        /// Checks if the entity exists for given criteria.
        /// </summary>
        /// <param name="criteria">The expression.</param>
        /// <returns>true when an entity matching the criteria exists, false otherwise.</returns>
        bool Exists(Expression<Func<T, bool>> criteria);
    }
}