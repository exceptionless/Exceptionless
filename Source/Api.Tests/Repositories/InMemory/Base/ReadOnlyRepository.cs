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
using System.Collections.ObjectModel;
using System.Linq;
using AutoMapper;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public abstract class ReadOnlyRepository<T> : IReadOnlyRepository<T> where T : class, IIdentity, new() {
        protected readonly Collection<T> _collection;

        protected ReadOnlyRepository(ICacheClient cacheClient = null) {
            _collection = new Collection<T>();
            Cache = cacheClient;
        }

        protected ICacheClient Cache { get; private set; }

        protected Collection<T> Collection { get { return _collection; } }

        protected virtual string GetCollectionName() {
            return typeof(T).Name.ToLower();
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

            if (Mapper.FindTypeMapFor<T, TModel>() == null)
                Mapper.CreateMap<T, TModel>();

            var query = options.ApplyFilter(Collection.AsQueryable());
            result = Mapper.Map<T, TModel>(query.FirstOrDefault());

            if (result != null && options.UseCache)
                Cache.Set(GetScopedCacheKey(options.CacheKey), result, options.GetCacheExpirationDate());

            return result;
        }

        protected bool Exists(OneOptions<T> options) {
            if (options == null)
                throw new ArgumentNullException("options");

            var query = options.ApplyFilter(Collection.AsQueryable());
            return query.Any();
        }

        protected ICollection<TModel> Find<TModel>(MultiOptions<T> options) where TModel : class, new() {
            if (options == null)
                throw new ArgumentNullException("options");

            ICollection<TModel> result = null;
            if (options.UseCache)
                result = Cache.Get<ICollection<TModel>>(GetScopedCacheKey(options.CacheKey));

            if (result != null)
                return result;

            var query = options.ApplyFilter(Collection.AsQueryable());
            if (options.UsePaging)
                query = query.Skip(options.GetSkip());
            if (options.UseLimit)
                query = query.Take(options.GetLimit() + 1);
            //if (options.Fields.Count > 0)
            //    cursor.SetFields(Fields.Include(options.Fields.ToArray()));
            //if (options.SortBy != null)
            //    query = query.OrderBy(options.SortBy);

            if (Mapper.FindTypeMapFor<T, TModel>() == null)
                Mapper.CreateMap<T, TModel>();

            result = new Collection<TModel>();
            foreach (var doc in query.ToList())
                result.Add(Mapper.Map<T, TModel>(doc));

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

            return FindOne<T>(new OneOptions<T>().WithIds<T>(id).WithCacheKey(useCache ? id : null).WithExpiresIn(expiresIn));
        }

        public ICollection<T> GetByIds(ICollection<string> ids, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (ids == null || ids.Count == 0)
                return new List<T>();

            string cacheKey = String.Join("", ids).GetHashCode().ToString();
            return Find<T>(FindOptionsExtensions.WithPaging(new MultiOptions().WithIds(ids), paging).WithCacheKey(useCache ? cacheKey : null).WithExpiresIn(expiresIn));
        }

        public bool Exists(string id) {
            if (String.IsNullOrEmpty(id))
                return false;

            return Exists(new OneOptions<T>().WithId(id));
        }
    }
}