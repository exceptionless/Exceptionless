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
using Exceptionless.Core.Caching;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Repositories {
    public abstract class MongoReadOnlyRepository<T> : IReadOnlyRepository<T>, IMongoRepositoryManagement where T : class, IIdentity, new() {
        protected MongoCollection<T> _collection;
        protected Func<string, BsonValue> _getIdValue = id => new BsonObjectId(new ObjectId(id));

        protected MongoReadOnlyRepository(MongoDatabase database, ICacheClient cacheClient = null) {
            Cache = cacheClient;
            InitializeCollection(database);
        }

        protected virtual void CreateCollection(MongoDatabase database) {
            database.CreateCollection(GetCollectionName());
        }

        protected ICacheClient Cache { get; private set; }

        protected MongoCollection<T> Collection { get { return _collection; } }

        protected virtual string GetCollectionName() {
            return typeof(T).Name.ToLower();
        }

        void IMongoRepositoryManagement.InitializeCollection(MongoDatabase database) {
            InitializeCollection(database);
        }

        MongoCollection IMongoRepositoryManagement.GetCollection() {
            return _collection;
        }

        string IMongoRepositoryManagement.GetCollectionName() {
            return GetCollectionName();
        }

        Type IMongoRepositoryManagement.GetDocumentType() {
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
            cm.SetIdMember(cm.GetMemberMap(c => c.Id).SetRepresentation(BsonType.ObjectId).SetIdGenerator(new StringObjectIdGenerator()));
        }

        public void InvalidateCache(string cacheKey) {
            if (Cache == null)
                return;

            Cache.Remove(GetScopedCacheKey(cacheKey));
        }

        public virtual void InvalidateCache(T document) {
            if (Cache == null)
                return;
            
            Cache.Remove(GetScopedCacheKey(document.Id));
        }

        protected string GetScopedCacheKey(string cacheKey) {
            return String.Concat(GetCollectionName(), "-", cacheKey);
        }

        protected TModel FindOne<TModel>(OneOptions options) where TModel : class, new() {
            if (options == null)
                throw new ArgumentNullException("options");

            TModel result = null;
            if (options.UseCache)
                result = Cache.Get<TModel>(GetScopedCacheKey(options.CacheKey));

            if (result != null)
                return result;

            var findArgs = new FindOneArgs { Query = options.GetQuery(_getIdValue), Fields = Fields.Include(options.Fields.ToArray()), SortBy = options.SortBy };
            if (options.ReadPreference != null)
                findArgs.ReadPreference = options.ReadPreference;

            result = _collection.FindOneAs<TModel>(findArgs);

            if (result != null && options.UseCache)
                Cache.Set(GetScopedCacheKey(options.CacheKey), result, options.GetCacheExpirationDate());

            return result;
        }

        protected bool Exists(OneOptions options) {
            if (options == null)
                throw new ArgumentNullException("options");

            var findArgs = new FindOneArgs {
                Query = options.GetQuery(_getIdValue),
                Fields = Fields.Include(CommonFieldNames.Id),
                ReadPreference = ReadPreference.Primary
            };

            return _collection.FindOneAs<T>(findArgs) != null;
        }

        protected ICollection<TModel> Find<TModel>(MultiOptions options) where TModel : class, new() {
            if (options == null)
                throw new ArgumentNullException("options");

            ICollection<TModel> result = null;
            if (options.UseCache)
                result = Cache.Get<ICollection<TModel>>(GetScopedCacheKey(options.CacheKey));

            if (result != null)
                return result;

            var cursor = _collection.FindAs<TModel>(options.GetQuery(_getIdValue));
            if (options.ReadPreference != null)
                cursor.SetReadPreference(options.ReadPreference);
            if (options.UsePaging)
                cursor.SetSkip(options.GetSkip());
            if (options.UseLimit)
                cursor.SetLimit(options.GetLimit() + 1);
            if (options.Fields.Count > 0)
                cursor.SetFields(Fields.Include(options.Fields.ToArray()));
            if (options.SortBy != null)
                cursor.SetSortOrder(options.SortBy);

            result = cursor.ToList();
            if (options.UseLimit) {
                if (result.Count > options.GetLimit())
                    options.HasMore = true;
                result = result.Take(options.GetLimit()).ToList();
            }

            if (options.UseCache)
                Cache.Set(GetScopedCacheKey(options.CacheKey), result, options.GetCacheExpirationDate());

            return result;
        }

        public long Count() {
            return _collection.Count();
        }

        public T GetById(string id, bool useCache = false, TimeSpan? expiresIn = null) {
            if (String.IsNullOrEmpty(id))
                return null;

            return FindOne<T>(new OneOptions().WithIds(id).WithCacheKey(useCache ? id : null).WithExpiresIn(expiresIn));
        }

        public ICollection<T> GetByIds(ICollection<string> ids, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (ids == null || ids.Count == 0)
                return new List<T>();

            string cacheKey = String.Join("", ids).GetHashCode().ToString();
            return Find<T>(new MultiOptions().WithIds(ids).WithPaging(paging).WithCacheKey(useCache ? cacheKey : null).WithExpiresIn(expiresIn));
        }

        public bool Exists(string id) {
            if (String.IsNullOrEmpty(id))
                return false;

            return Exists(new OneOptions().WithId(id));
        }
    }
}