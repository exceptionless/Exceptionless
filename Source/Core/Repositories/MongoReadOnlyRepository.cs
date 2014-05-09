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
using Exceptionless.Core.Extensions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using DbQuery = MongoDB.Driver.Builders.Query;

namespace Exceptionless.Core.Repositories {
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
        /// Finds a T using the given query.
        /// </summary>
        /// <param name="query">The query to use.</param>
        /// <param name="cacheKey">If specified, will allow the document to be read from cache.</param>
        /// <returns>The Entity T.</returns>
        public TModel FindOne<TModel>(IMongoQuery query, string cacheKey = null) where TModel: class, new() {
            if (query == null)
                throw new ArgumentNullException("query");

            TModel result = null;
            if (!String.IsNullOrEmpty(cacheKey))
                result = Cache.Get<TModel>(GetScopedCacheKey(cacheKey));

            if (result != null)
                return result;

            var findArgs = new FindOneArgs { Query = query };
            if (!String.IsNullOrEmpty(cacheKey))
                findArgs.ReadPreference = ReadPreference.Primary;

            result = _collection.FindOneAs<TModel>(findArgs);

            if (result != null)
                Cache.Set(GetScopedCacheKey(cacheKey), result);

            return result;
        }

        /// <summary>
        /// Find all documents matching the specified query.
        /// </summary>
        /// <param name="options">The <see cref="FindOptions"/> to use when finding the entities.</param>
        /// <returns>An IList<see cref="T" />> with entities that match the criteria.</returns>
        protected IList<TModel> Find<TModel>(FindOptions options) where TModel : class, new() {
            if (options == null)
                throw new ArgumentNullException("options");

            IList<TModel> result = null;
            if (options.UseCache)
                result = Cache.Get<IList<TModel>>(GetScopedCacheKey(options.CacheKey));

            if (result != null)
                return result;

            var cursor = _collection.FindAs<TModel>(options.GetQuery());
            if (!options.UseCache)
                cursor.SetReadPreference(ReadPreference.Primary);
            if (options.UsePaging)
                cursor.SetSkip(options.GetSkip());
            if (options.UseLimit)
                cursor.SetLimit(options.GetLimit() + 1);
            if (options.Fields != null)
                cursor.SetFields(options.Fields);
            if (options.SortBy != null)
                cursor.SetSortOrder(options.SortBy);

            result = cursor.ToList();
            if (options.UseLimit) {
                if (result.Count > options.GetLimit())
                    options.HasMore = true;
                result = result.Take(options.GetLimit()).ToList();
            }

            Cache.Set(GetScopedCacheKey(options.CacheKey), result);

            return result;
        }

        /// <summary>
        /// Returns a single T by the given criteria.
        /// </summary>
        /// <param name="criteria">The expression.</param>
        /// <returns>A single T matching the criteria.</returns>
        public T FirstOrDefault(Expression<Func<T, bool>> criteria) {
            var query = GetQueryFromExpression(criteria);
            Collection.FindOne(query);
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

    public class FindOptions {
        public FindOptions() {
            Ids = new List<string>();
            OrganizationIds = new List<string>();
            ProjectIds = new List<string>();
            StackIds = new List<string>();
        }

        public List<string> Ids { get; set; }
        public List<string> OrganizationIds { get; set; }
        public List<string> ProjectIds { get; set; }
        public List<string> StackIds { get; set; }
        public bool HasMore { get; set; }
        public IMongoQuery Query { get; set; }
        public IMongoFields Fields { get; set; }
        public IMongoSortBy SortBy { get; set; }
        public string BeforeValue { get; set; }
        public IMongoQuery BeforeQuery { get; set; }
        public string AfterValue { get; set; }
        public IMongoQuery AfterQuery { get; set; }
        public int? Limit { get; set; }
        public int? Page { get; set; }
        public string CacheKey { get; set; }

        public bool UseLimit {
            get { return Limit.HasValue; }
        }

        public bool UseSkip {
            get { return UsePaging; }
        }

        public bool UseCache {
            get { return !String.IsNullOrEmpty(CacheKey); }
        }

        public bool UsePaging {
            get { return Page.HasValue; }
        }

        public const int DEFAULT_LIMIT = 10;
        public const int MAX_LIMIT = 100;
        public int GetLimit() {
            if (!Limit.HasValue || Limit.Value < 1)
                return DEFAULT_LIMIT;

            if (Limit.Value > MAX_LIMIT)
                return MAX_LIMIT;

            return Limit.Value;
        }

        public int GetSkip() {
            if (!Page.HasValue || Page.Value < 1)
                return 0;

            int skip = (Page.Value - 1) * GetLimit();
            if (skip < 0)
                skip = 0;

            return skip;
        }

        public IMongoQuery GetQuery() {
            IMongoQuery query = Query;
            if (Ids.Count > 0)
                query = query.And(DbQuery.In(CommonFieldNames.Id, Ids.Select(id => new BsonObjectId(new ObjectId(id)))));
            if (OrganizationIds.Count > 0)
                query = query.And(DbQuery.In(CommonFieldNames.OrganizationId, OrganizationIds.Select(id => new BsonObjectId(new ObjectId(id)))));
            if (ProjectIds.Count > 0)
                query = query.And(DbQuery.In(CommonFieldNames.ProjectId, ProjectIds.Select(id => new BsonObjectId(new ObjectId(id)))));
            if (StackIds.Count > 0)
                query = query.And(DbQuery.In(CommonFieldNames.StackId, StackIds.Select(id => new BsonObjectId(new ObjectId(id)))));

            if (Page.HasValue)
                return query;

            if (!String.IsNullOrEmpty(BeforeValue) && BeforeQuery == null)
                BeforeQuery = DbQuery.LT(CommonFieldNames.Id, ObjectId.Parse(BeforeValue));

            if (!String.IsNullOrEmpty(AfterValue) && AfterQuery == null)
                AfterQuery = DbQuery.LT(CommonFieldNames.Id, ObjectId.Parse(AfterValue));

            query = query.And(BeforeQuery);
            query = query.And(AfterQuery);

            return query;
        }
    }
}