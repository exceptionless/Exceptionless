using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Caching;
using MongoDB.Bson;
using Nest;
using NLog.Fluent;

namespace Exceptionless.Core.Repositories {
    public abstract class ElasticSearchReadOnlyRepository<T> : IReadOnlyRepository<T> where T : class, IIdentity, new() {
        protected readonly static string _entityType = typeof(T).Name;
        private static readonly DateTime MIN_OBJECTID_DATE = new DateTime(2000, 1, 1);
        protected readonly IElasticClient _elasticClient;
        protected static readonly bool _isEvent = typeof(T) == typeof(PersistentEvent);
        protected static readonly bool _isStack = typeof(T) == typeof(Stack);
        public static string EventsIndexName = "events-v1";
        public static string StacksIndexName = "stacks-v1";

        protected ElasticSearchReadOnlyRepository(IElasticClient elasticClient, ICacheClient cacheClient = null) {
            _elasticClient = elasticClient;
            Cache = cacheClient;
            EnableCache = cacheClient != null;
        }

        public bool EnableCache { get; protected set; }

        protected ICacheClient Cache { get; private set; }

        protected virtual string GetTypeName() {
            return _entityType.ToLower();
        }

        public void InvalidateCache(string cacheKey) {
            if (!EnableCache || Cache == null)
                return;

            Cache.Remove(GetScopedCacheKey(cacheKey));
        }

        public virtual void InvalidateCache(T document) {
            if (!EnableCache || Cache == null)
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

            if (EnableCache) {
                if (options.UseCache)
                    result = Cache.Get<TModel>(GetScopedCacheKey(options.CacheKey));

                if (options.UseCache && result != null)
                    Log.Trace().Message("Cache hit: type={0}", _entityType).Write();
                else if (options.UseCache)
                    Log.Trace().Message("Cache miss: type={0}", _entityType).Write();

                if (result != null)
                    return result;
            }

            var searchDescriptor = new SearchDescriptor<T>().Filter(options.GetElasticSearchFilter<T>()).Size(1);
            if (options.Fields.Count > 0)
                searchDescriptor.Source(s => s.Include(options.Fields.ToArray()));
            else
                searchDescriptor.Source(s => s.Exclude("idx"));
            
            var elasticSearchOptions = options as ElasticSearchOptions<T>;
            if (elasticSearchOptions != null && elasticSearchOptions.SortBy.Count > 0) {
                searchDescriptor.Indices(elasticSearchOptions.Indices);
                foreach (var sort in elasticSearchOptions.SortBy)
                    searchDescriptor.Sort(sort);
            }

            _elasticClient.EnableTrace();
            var item = _elasticClient.Search<T>(searchDescriptor).Documents.FirstOrDefault();
            _elasticClient.DisableTrace();

            if (typeof(T) != typeof(TModel)) {
                if (Mapper.FindTypeMapFor<T, TModel>() == null)
                    Mapper.CreateMap<T, TModel>();

                result = Mapper.Map<T, TModel>(item);
            } else {
                result = item as TModel;
            }

            if (EnableCache && result != null && options.UseCache)
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

        protected long Count(ElasticSearchOptions<T> options) {
            if (options == null)
                throw new ArgumentNullException("options");

            long? result;
            if (EnableCache && options.UseCache) {
                result = Cache.Get<long?>(GetScopedCacheKey("count-" + options.CacheKey));
                if (result.HasValue)
                    return result.Value;
            }

            var countDescriptor = new CountDescriptor<T>().Query(f => f.Filtered(s => s.Filter(f2 => options.GetElasticSearchFilter())));
            countDescriptor.Indices(options.Indices);
            countDescriptor.IgnoreUnavailable();

            countDescriptor.Type(typeof(T));

            _elasticClient.EnableTrace();
            var results = _elasticClient.Count<T>(countDescriptor);
            _elasticClient.DisableTrace();

            if (!results.IsValid)
                throw new ApplicationException(String.Format("ElasticSearch error code \"{0}\".", results.ConnectionStatus.HttpStatusCode), results.ConnectionStatus.OriginalException);

            result = results.Count;

            if (EnableCache && options.UseCache)
                Cache.Set(GetScopedCacheKey("count-" + options.CacheKey), result, options.GetCacheExpirationDate());

            return result.Value;
        }

        protected ICollection<T> Find(ElasticSearchOptions<T> options) {
            return FindAs<T>(options);
        }

        protected ICollection<TModel> FindAs<TModel>(ElasticSearchOptions<T> options) where TModel : class, new() {
            if (options == null)
                throw new ArgumentNullException("options");

            ICollection<TModel> result;
            if (EnableCache && options.UseCache) {
                result = Cache.Get<ICollection<TModel>>(GetScopedCacheKey(options.CacheKey));
                if (result != null)
                    return result;
            }

            var searchDescriptor = new SearchDescriptor<T>().Filter(options.GetElasticSearchFilter());
            searchDescriptor.Indices(options.Indices);
            searchDescriptor.IgnoreUnavailable();

            if (options.UsePaging)
                searchDescriptor.Skip(options.GetSkip());
            searchDescriptor.Size(options.GetLimit());
            searchDescriptor.Type(typeof(T));
            if (options.Fields.Count > 0)
                searchDescriptor.Source(s => s.Include(options.Fields.ToArray()));
            else
                searchDescriptor.Source(s => s.Exclude("idx"));
            if (options.SortBy.Count > 0)
                foreach (var sort in options.SortBy)
                    searchDescriptor.Sort(sort);
            
            _elasticClient.EnableTrace();
            var results = _elasticClient.Search<T>(searchDescriptor);
            _elasticClient.DisableTrace();

            if (!results.IsValid)
                throw new ApplicationException(String.Format("ElasticSearch error code \"{0}\".", results.ConnectionStatus.HttpStatusCode), results.ConnectionStatus.OriginalException);

            options.HasMore = options.UseLimit && results.Total > options.GetLimit();

            var items = results.Documents.ToList();

            if (typeof(T) != typeof(TModel)) {
                if (Mapper.FindTypeMapFor<T, TModel>() == null)
                    Mapper.CreateMap<T, TModel>();

                result = Enumerable.ToList(items.Select(Mapper.Map<T, TModel>));
            } else {
                result = items as List<TModel>;
            }

            if (EnableCache && options.UseCache)
                Cache.Set(GetScopedCacheKey(options.CacheKey), result, options.GetCacheExpirationDate());

            return result;
        }

        public long Count() {
            return _elasticClient.Count<T>(c => c.Query(q => q.MatchAll())).Count;
        }

        public T GetById(string id, bool useCache = false, TimeSpan? expiresIn = null) {
            if (String.IsNullOrEmpty(id))
                return null;

            T result = null;
            if (EnableCache && useCache) {
                result = Cache.Get<T>(GetScopedCacheKey(id));
                if (result != null)
                    return result;
            }

            // try using the object id to figure out what index the entity is located in
            string index = GetIndexName(id);
            if (index != null) {
                _elasticClient.EnableTrace();
                result = _elasticClient.Get<T>(f => f.Id(id).Index(index).SourceExclude("idx")).Source;
                _elasticClient.DisableTrace();
            }

            // fallback to doing a find
            if (result == null)
                result = FindOne(new OneOptions().WithId(id).WithCacheKey(EnableCache && useCache ? id : null).WithExpiresIn(expiresIn));

            if (EnableCache && result != null && useCache)
                Cache.Set(GetScopedCacheKey(id), result, expiresIn ?? TimeSpan.FromSeconds(RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS));

            return result;
        }

        public ICollection<T> GetByIds(ICollection<string> ids, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (ids == null || ids.Count == 0)
                return new List<T>();

            var results = new List<T>();
            if (EnableCache && useCache) {
                results.AddRange(ids.Select(id => Cache.Get<T>(GetScopedCacheKey(id))).Where(cacheHit => cacheHit != null));

                var notCachedIds = ids.Except(results.Select(i => i.Id)).ToArray();
                if (notCachedIds.Length == 0)
                    return results;
            }

            // try using the object id to figure out what index the entity is located in
            var foundItems = new List<T>();
            var itemsToFind = new List<string>();
            var multiGet = new MultiGetDescriptor();
            foreach (var id in ids.Except(results.Select(i => i.Id))) {
                string index = GetIndexName(id);
                if (index != null)
                    multiGet.Get<T>(f => f.Id(id).Index(index).Source(s => s.Exclude("idx")));
                else
                    itemsToFind.Add(id);
            }

            _elasticClient.EnableTrace();
            foreach (var doc in _elasticClient.MultiGet(multiGet).Documents) {
                if (doc.Found)
                    foundItems.Add(doc.Source as T);
                else
                    itemsToFind.Add(doc.Id);
            }
            _elasticClient.DisableTrace();

            // fallback to doing a find
            if (itemsToFind.Count > 0)
                foundItems.AddRange(Find(new ElasticSearchOptions<T>().WithIds(itemsToFind)));

            if (EnableCache && useCache && foundItems.Count > 0) {
                foreach (var item in foundItems)
                    Cache.Set(GetScopedCacheKey(item.Id), item, expiresIn.HasValue ? DateTime.Now.Add(expiresIn.Value) : DateTime.Now.AddSeconds(RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS));
            }

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