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
using Nest;

namespace Exceptionless.Core.Repositories {
    public abstract class ElasticSearchReadOnlyRepository<T> : IReadOnlyRepository<T> where T : class, IIdentity, new() {
        protected readonly IElasticClient _elasticClient;

        protected ElasticSearchReadOnlyRepository(IElasticClient elasticClient, ICacheClient cacheClient = null) {
            _elasticClient = elasticClient;
            Cache = cacheClient;
        }

        protected ICacheClient Cache { get; private set; }

        protected virtual string GetTypeName() {
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
            return String.Concat(GetTypeName(), "-", cacheKey);
        }

        protected TModel FindOne<TModel>(OneOptions options) where TModel : class, new() {
            if (options == null)
                throw new ArgumentNullException("options");

            TModel result = null;
            if (options.UseCache)
                result = Cache.Get<TModel>(GetScopedCacheKey(options.CacheKey));

            if (result != null)
                return result;

            var searchDescriptor = new SearchDescriptor<TModel>().Filter(options.GetElasticSearchFilter<T>()).Take(1);
            if (options.Fields.Count > 0)
                searchDescriptor.Source(s => s.Include(options.Fields.ToArray()));

            var elasticSearchOptions = options as ElasticSearchOptions<TModel>;
            if (elasticSearchOptions != null && elasticSearchOptions.SortBy.Count > 0) {
                searchDescriptor.Indices(elasticSearchOptions.Indices);
                foreach (var sort in elasticSearchOptions.SortBy)
                    searchDescriptor.Sort(sort);
            }

            result = _elasticClient.Search<TModel>(searchDescriptor).Documents.FirstOrDefault();
            if (result != null && options.UseCache)
                Cache.Set(GetScopedCacheKey(options.CacheKey), result, options.GetCacheExpirationDate());

            return result;
        }

        protected bool Exists(OneOptions options) {
            if (options == null)
                throw new ArgumentNullException("options");

            options.Fields.Add("id");
            var searchDescriptor = new SearchDescriptor<T>().Filter(options.GetElasticSearchFilter<T>()).Size(1);

            var elasticSearchOptions = options as ElasticSearchOptions<T>;
            if (elasticSearchOptions != null && elasticSearchOptions.SortBy.Count > 0) {
                searchDescriptor.Indices(elasticSearchOptions.Indices);
                foreach (var sort in elasticSearchOptions.SortBy)
                    searchDescriptor.Sort(sort);
            }

            return _elasticClient.Search<T>(searchDescriptor).HitsMetaData.Total > 0;
        }

        protected ICollection<TModel> Find<TModel>(ElasticSearchOptions<TModel> options) where TModel : class, new() {
            if (options == null)
                throw new ArgumentNullException("options");

            ICollection<TModel> result = null;
            if (options.UseCache)
                result = Cache.Get<ICollection<TModel>>(GetScopedCacheKey(options.CacheKey));

            if (result != null)
                return result;

            var searchDescriptor = new SearchDescriptor<TModel>().Filter(options.GetElasticSearchFilter());
            searchDescriptor.Indices(options.Indices);

            if (options.UsePaging)
                searchDescriptor.Skip(options.GetSkip());
            searchDescriptor.Size(options.GetLimit());
            if (options.Fields.Count > 0)
                searchDescriptor.Source(s => s.Include(options.Fields.ToArray()));
            if (options.SortBy.Count > 0)
                foreach (var sort in options.SortBy)
                    searchDescriptor.Sort(sort);

            var results = _elasticClient.Search<TModel>(searchDescriptor);
            options.HasMore = options.UseLimit && results.HitsMetaData.Total > options.GetLimit();

            result = results.Documents.ToList();
            if (options.UseCache)
                Cache.Set(GetScopedCacheKey(options.CacheKey), result, options.GetCacheExpirationDate());

            return result;
        }

        public long Count() {
            return _elasticClient.Count<T>(c => c.Query(q => q.MatchAll())).Count;
        }

        public T GetById(string id, bool useCache = false, TimeSpan? expiresIn = null) {
            if (String.IsNullOrEmpty(id))
                return null;

            return FindOne<T>(new OneOptions().WithId(id).WithCacheKey(useCache ? id : null).WithExpiresIn(expiresIn));
        }

        public ICollection<T> GetByIds(ICollection<string> ids, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (ids == null || ids.Count == 0)
                return new List<T>();

            var results = new List<T>();
            if (useCache)
                results.AddRange(ids.Select(id => Cache.Get<T>(GetScopedCacheKey(id))).Where(cacheHit => cacheHit != null));

            var notCachedIds = ids.Except(results.Select(i => i.Id)).ToArray();
            if (notCachedIds.Length == 0)
                return results;

            var foundItems = Find(new ElasticSearchOptions<T>().WithIds(ids.Except(results.Select(i => i.Id))));
            
            if (useCache && foundItems.Count > 0)
                foreach (var item in foundItems)
                    Cache.Set(GetScopedCacheKey(item.Id), item, expiresIn.HasValue ? DateTime.Now.Add(expiresIn.Value) : DateTime.Now.AddSeconds(RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS));

            results.AddRange(foundItems);

            return results;
        }

        public bool Exists(string id) {
            if (String.IsNullOrEmpty(id))
                return false;

            return Exists(new OneOptions().WithId(id));
        }
    }
}