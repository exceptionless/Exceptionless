#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Exceptionless.Core.Caching;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Exceptionless.Core {
    /// <summary>
    /// Deals with entities in MongoDb.
    /// </summary>
    /// <typeparam name="T">The type contained in the repository.</typeparam>
    public abstract class MongoReadOnlyRepository<T> : IReadOnlyRepository<T>, IMongoRepositoryManagement where T : class, new() {
        /// <summary>
        /// MongoCollection field.
        /// </summary>
        protected MongoCollection<T> _collection;

        /// <summary>
        /// Initializes a new instance of the MongoRepository class.
        /// Uses the Default App/Web.Config connection strings to fetch the connectionString and Database name.
        /// </summary>
        protected MongoReadOnlyRepository(MongoDatabase database, ICacheClient cacheClient = null) {
            Cache = cacheClient;
            InitializeCollection(database);
        }

        protected virtual void CreateCollection(MongoDatabase database) {
            database.CreateCollection(GetCollectionName());
        }

        protected ICacheClient Cache { get; private set; }

        public MongoCollection<T> Collection { get { return _collection; } }

        protected virtual string GetCollectionName() {
            return typeof(T).Name.ToLower();
        }

        protected abstract string GetId(T entity);

        void IMongoRepositoryManagement.InitializeCollection(MongoDatabase database) {
            InitializeCollection(database);
        }

        MongoCollection IMongoRepositoryManagement.GetCollection() {
            return _collection;
        }

        string IMongoRepositoryManagement.GetCollectionName() {
            return GetCollectionName();
        }

        Type IMongoRepositoryManagement.GetEntityType() {
            return typeof(T);
        }

        protected virtual void InitializeCollection(MongoDatabase database) {
            if (!database.CollectionExists(GetCollectionName()))
                CreateCollection(database);

            _collection = database.GetCollection<T>(GetCollectionName());

            if (!BsonClassMap.IsClassMapRegistered(typeof(T)))
                BsonClassMap.RegisterClassMap<T>(ConfigureClassMap);
        }

        protected virtual void ConfigureClassMap(BsonClassMap<T> cm) {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
        }

        protected IMongoQuery GetQueryFromExpression(Expression<Func<T, bool>> criteria) {
            var provider = All().Provider as MongoQueryProvider;
            if (provider == null)
                return null;

            // TODO: We need to log a mongo issue for the Where clause not respecting the mongo SetRepresentation ObjectId on ObjectID fields.
            return provider.BuildMongoQuery(All().Where(criteria) as MongoQueryable<T>);
        }

        /// <summary>
        /// Checks if the entity exists for given criteria.
        /// </summary>
        /// <param name="criteria">The expression.</param>
        /// <returns>true when an entity matching the criteria exists, false otherwise.</returns>
        public bool Exists(Expression<Func<T, bool>> criteria) {
            return All().Any(criteria);
        }

        public void InvalidateCache(string cacheKey) {
            if (Cache == null)
                return;

            Cache.Remove(GetScopedCacheKey(cacheKey));
        }

        public virtual void InvalidateCache(T entity) {
            if (Cache == null)
                return;

            Cache.Remove(GetScopedCacheKey(GetId(entity)));
        }

        protected string GetScopedCacheKey(string cacheKey) {
            return String.Concat(GetCollectionName(), "-", cacheKey);
        }

        /// <summary>
        /// Returns a single T by the given criteria.
        /// </summary>
        /// <param name="criteria">The expression.</param>
        /// <returns>A single T matching the criteria.</returns>
        public T FirstOrDefault(Expression<Func<T, bool>> criteria) {
            return All().Where(criteria).FirstOrDefault();
        }

        public T FirstOrDefaultCached(Expression<Func<T, bool>> criteria, string cacheKey, TimeSpan? expiresIn = null, DateTime? expiresAt = null) {
            var result = Cache.Get<T>(GetScopedCacheKey(cacheKey));
            if (result == null) {
                result = FirstOrDefault(criteria);
                if (result != null) {
                    if (!expiresIn.HasValue)
                        expiresIn = TimeSpan.FromMinutes(5);

                    if (expiresAt.HasValue)
                        Cache.Set(GetScopedCacheKey(cacheKey), result, expiresAt.Value);
                    else
                        Cache.Set(GetScopedCacheKey(cacheKey), result, expiresIn.Value);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns a IQueryable of T by the given query.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>All entities matching the query.</returns>
        public IQueryable<T> Where(IMongoQuery query) {
            return _collection.FindAs<T>(query).AsQueryable();
        }

        public IQueryable<T> WhereCached(IMongoQuery query, string cacheKey, TimeSpan? expiresIn = null, DateTime? expiresAt = null) {
            var result = Cache.Get<List<T>>(GetScopedCacheKey(cacheKey));
            if (result == null) {
                result = Where(query).ToList();
                if (!expiresIn.HasValue)
                    expiresIn = TimeSpan.FromMinutes(5);

                if (expiresAt.HasValue)
                    Cache.Set(GetScopedCacheKey(cacheKey), result, expiresAt.Value);
                else
                    Cache.Set(GetScopedCacheKey(cacheKey), result, expiresIn.Value);
            }

            return result.AsQueryable();
        }

        /// <summary>
        /// Returns All the records of T.
        /// </summary>
        /// <returns>IQueryable of T.</returns>
        public IQueryable<T> All() {
            return _collection.AsQueryable();
        }

        /// <summary>
        /// Counts the total entities in the repository.
        /// </summary>
        /// <returns>Count of entities in the collection.</returns>
        public long Count() {
            return _collection.Count();
        }

        /// <summary>
        /// Counts the total entities in the repository by the given query.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>A count of all entities matching the query.</returns>
        public long Count(IMongoQuery query) {
            return _collection.FindAs<T>(query).Count();
        }

        /// <summary>
        /// Returns a IQueryable of T by the given criteria.
        /// </summary>
        /// <param name="criteria">The expression.</param>
        /// <returns>A single T matching the criteria.</returns>
        public IQueryable<T> Where(Expression<Func<T, bool>> criteria) {
            return All().Where(criteria);
        }

        public IQueryable<T> WhereCached(Expression<Func<T, bool>> criteria, string cacheKey, TimeSpan? expiresIn = null, DateTime? expiresAt = null) {
            var result = Cache.Get<List<T>>(GetScopedCacheKey(cacheKey));
            if (result == null) {
                result = Where(criteria).ToList();
                if (!expiresIn.HasValue)
                    expiresIn = TimeSpan.FromMinutes(5);

                if (expiresAt.HasValue)
                    Cache.Set(GetScopedCacheKey(cacheKey), result, expiresAt.Value);
                else
                    Cache.Set(GetScopedCacheKey(cacheKey), result, expiresIn.Value);
            }

            return result.AsQueryable();
        }
    }
}