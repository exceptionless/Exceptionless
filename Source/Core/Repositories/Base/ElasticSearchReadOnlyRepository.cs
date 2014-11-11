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
using System.Diagnostics;
using System.Linq;
using AutoMapper;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using MongoDB.Bson;
using Nest;
using NLog.Fluent;

namespace Exceptionless.Core.Repositories {
    public abstract class ElasticSearchReadOnlyRepository<T> : IReadOnlyRepository<T> where T : class, IIdentity, new() {
        private static readonly DateTime MIN_OBJECTID_DATE = new DateTime(2000, 1, 1);
        protected readonly IElasticClient _elasticClient;
        protected static readonly bool _isEvent = typeof(T) == typeof(PersistentEvent);
        protected static readonly bool _isStack = typeof(T) == typeof(Stack);
        public static string EventsIndexName = "events-v1";
        public static string StacksIndexName = "stacks-v1";

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

        protected T FindOne(OneOptions options) {
            return FindOneAs<T>(options);
        }

        protected TModel FindOneAs<TModel>(OneOptions options) where TModel : class, new() {
            if (options == null)
                throw new ArgumentNullException("options");

            TModel result = null;
            if (options.UseCache)
                result = Cache.Get<TModel>(GetScopedCacheKey(options.CacheKey));

            if (options.UseCache && result != null)
                Log.Info().Message("Cache hit: type={1}", typeof(T).Name);
            else if (options.UseCache)
                Log.Info().Message("Cache miss: type={1}", typeof(T).Name);

            if (result != null)
                return result;

            var searchDescriptor = new SearchDescriptor<T>().Filter(options.GetElasticSearchFilter<T>()).Size(1);
            if (options.Fields.Count > 0)
                searchDescriptor.Source(s => s.Include(options.Fields.ToArray()));

            var elasticSearchOptions = options as ElasticSearchOptions<T>;
            if (elasticSearchOptions != null && elasticSearchOptions.SortBy.Count > 0) {
                searchDescriptor.Indices(elasticSearchOptions.Indices);
                foreach (var sort in elasticSearchOptions.SortBy)
                    searchDescriptor.Sort(sort);
            }

            var item = _elasticClient.Search<T>(searchDescriptor).Documents.FirstOrDefault();

            if (typeof(T) != typeof(TModel)) {
                if (Mapper.FindTypeMapFor<T, TModel>() == null)
                    Mapper.CreateMap<T, TModel>();

                result = Mapper.Map<T, TModel>(item);
            } else {
                result = item as TModel;
            }

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

        protected ICollection<T> Find(ElasticSearchOptions<T> options) {
            return FindAs<T>(options);
        }

        protected ICollection<TModel> FindAs<TModel>(ElasticSearchOptions<T> options) where TModel : class, new() {
            if (options == null)
                throw new ArgumentNullException("options");

            ICollection<TModel> result = null;
            if (options.UseCache)
                result = Cache.Get<ICollection<TModel>>(GetScopedCacheKey(options.CacheKey));

            if (result != null)
                return result;

            var searchDescriptor = new SearchDescriptor<T>().Filter(options.GetElasticSearchFilter());
            searchDescriptor.Indices(options.Indices);
            searchDescriptor.IgnoreUnavailable();

            if (options.UsePaging)
                searchDescriptor.Skip(options.GetSkip());
            searchDescriptor.Size(options.GetLimit());
            searchDescriptor.Type(typeof(T));
            if (options.Fields.Count > 0)
                searchDescriptor.Source(s => s.Include(options.Fields.ToArray()));
            if (options.SortBy.Count > 0)
                foreach (var sort in options.SortBy)
                    searchDescriptor.Sort(sort);

            _elasticClient.EnableTrace();
            var results = _elasticClient.Search<T>(searchDescriptor);
            _elasticClient.DisableTrace();
            if (!results.IsValid)
                throw new ApplicationException("Error occurred processing request.");

            Debug.WriteLine("Results: " + results.Total);
            options.HasMore = options.UseLimit && results.Total > options.GetLimit();

            var items = results.Documents.ToList();

            if (typeof(T) != typeof(TModel)) {
                if (Mapper.FindTypeMapFor<T, TModel>() == null)
                    Mapper.CreateMap<T, TModel>();

                result = items.Select(Mapper.Map<T, TModel>).ToList();
            } else {
                result = items as List<TModel>;
            }

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

            // try using the object id to figure out what index the entity is located in
            string index = GetIndexName(id);
            T result = null;
            if (index != null) {
                result = _elasticClient.Get<T>(id, index).Source;
                if (result != null && useCache)
                    Cache.Set(GetScopedCacheKey(id), result, expiresIn != null ? expiresIn.Value : TimeSpan.FromSeconds(RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS));
            }

            // fallback to doing a find
            if (result == null)
                result = FindOne(new OneOptions().WithId(id).WithCacheKey(useCache ? id : null).WithExpiresIn(expiresIn));

            return result;
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

            // try using the object id to figure out what index the entity is located in
            var foundItems = new List<T>();
            var itemsToFind = new List<string>();
            var multiGet = new MultiGetDescriptor();
            foreach (var id in ids.Except(results.Select(i => i.Id))) {
                string index = GetIndexName(id);
                if (index != null)
                    multiGet.Get<T>(f => f.Id(id).Index(index));
                else
                    itemsToFind.Add(id);
            }

            foreach (var doc in _elasticClient.MultiGet(multiGet).Documents) {
                if (doc.Found)
                    foundItems.Add(doc.Source as T);
                else
                    itemsToFind.Add(doc.Id);
            }

            // fallback to doing a find
            if (itemsToFind.Count > 0)
                foundItems.AddRange(Find(new ElasticSearchOptions<T>().WithIds(itemsToFind)));
            
            if (useCache && foundItems.Count > 0)
                foreach (var item in foundItems)
                    Cache.Set(GetScopedCacheKey(item.Id), item, expiresIn.HasValue ? DateTime.Now.Add(expiresIn.Value) : DateTime.Now.AddSeconds(RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS));

            results.AddRange(foundItems);

            return results;
        }

        private string GetIndexName(string id) {
            string index = null;
            if (_isEvent) {
                ObjectId objectId;
                if (ObjectId.TryParse(id, out objectId) && objectId.CreationTime > MIN_OBJECTID_DATE)
                    index = String.Concat(EventsIndexName, "-", objectId.CreationTime.ToString("yyyyMM"));
            } else if (_isStack) {
                index = StacksIndexName;
            }

            return index;
        }

        public bool Exists(string id) {
            if (String.IsNullOrEmpty(id))
                return false;

            return Exists(new OneOptions().WithId(id));
        }
    }
}