using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Caching;
using Nest;
using NLog.Fluent;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Exceptionless.Core.Component;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Utility;

namespace Exceptionless.Core.Repositories {
    public class FindResults<T> {
        public FindResults() {
            Documents = new List<T>();
        } 

        public ICollection<T> Documents { get; set; }
        public long Total { get; set; }
    }

    public abstract class ElasticSearchReadOnlyRepository<T> : IReadOnlyRepository<T> where T : class, IIdentity, new() {
        protected readonly static bool _supportsSoftDeletes = typeof(ISupportSoftDeletes).IsAssignableFrom(typeof(T));
        private static readonly DateTime MIN_OBJECTID_DATE = new DateTime(2000, 1, 1);
        protected static readonly string _entityType = typeof(T).Name;
        protected static readonly bool _isEvent = typeof(T) == typeof(PersistentEvent);
        protected static readonly bool _isStack = typeof(T) == typeof(Stack);

        protected readonly IElasticClient _elasticClient;
        protected readonly IElasticSearchIndex _index;

        protected ElasticSearchReadOnlyRepository(IElasticClient elasticClient, IElasticSearchIndex index, ICacheClient cacheClient = null) {
            _elasticClient = elasticClient;
            _index = index;
            Cache = cacheClient;
            EnableCache = cacheClient != null;
        }

        public bool EnableCache { get; protected set; }

        protected ICacheClient Cache { get; private set; }

        protected virtual string[] GetIndices() {
            return _index != null ? new[] { _index.Name } : new string[0];
        }

        protected virtual string GetTypeName() {
            return _entityType.ToLower();
        }

        protected Task InvalidateCacheAsync(string cacheKey, bool autoScopeCacheKey = true) {
            if (!EnableCache || Cache == null)
                return TaskHelper.Completed();

            return Cache.RemoveAsync(autoScopeCacheKey ? GetScopedCacheKey(cacheKey) : cacheKey);
        }

        protected virtual Task InvalidateCacheAsync(ICollection<T> documents, ICollection<T> originalDocuments) {
            if (!EnableCache || Cache == null)
                return TaskHelper.Completed();

            if (documents == null)
                throw new ArgumentNullException(nameof(documents));

            documents.ForEach(async d => await Cache.RemoveAsync(GetScopedCacheKey(d.Id)).AnyContext());
            return TaskHelper.Completed();
        }

        public Task InvalidateCacheAsync(T document) {
            return InvalidateCacheAsync(new[] { document });
        }

        public Task InvalidateCacheAsync(ICollection<T> documents) {
            return InvalidateCacheAsync(documents, null);
        }

        protected string GetScopedCacheKey(string cacheKey) {
            return String.Concat(GetTypeName(), "-", cacheKey);
        }

        protected async Task<FindResults<T>> FindAsync(ElasticSearchOptions<T> options) {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            
            if (EnableCache && options.UseCache) {
                var cacheValue = await Cache.GetAsync<FindResults<T>>(GetScopedCacheKey(options.CacheKey)).AnyContext();
#if DEBUG
                Log.Trace().Message("Cache {0}: type={1}", cacheValue.HasValue ? "hit" : "miss", _entityType).Write();
#endif
                if (cacheValue.HasValue)
                    return cacheValue.Value;
            }
            
            var searchDescriptor = options.SortBy.Count == 0 ?
                new SearchDescriptor<T>().Query(options.GetElasticSearchQuery(_supportsSoftDeletes))
                : new SearchDescriptor<T>().Filter(options.GetElasticSearchFilter(_supportsSoftDeletes));

            searchDescriptor.Indices(options.Indices.Any() ? options.Indices.ToArray() : GetIndices());
            searchDescriptor.IgnoreUnavailable();
            searchDescriptor.Size(options.GetLimit());
            searchDescriptor.Type(typeof(T));

            if (options.UsePaging)
                searchDescriptor.Skip(options.GetSkip());

            if (options.Fields.Count > 0)
                searchDescriptor.Source(s => s.Include(options.Fields.ToArray()));
            else
                searchDescriptor.Source(s => s.Exclude("idx"));

            if (options.SortBy.Count > 0)
                foreach (var sort in options.SortBy)
                    searchDescriptor.Sort(sort);

            _elasticClient.EnableTrace();
            var results = await _elasticClient.SearchAsync<T>(searchDescriptor).AnyContext();
            _elasticClient.DisableTrace();

            if (!results.IsValid)
                throw new ApplicationException($"ElasticSearch error code \"{results.ConnectionStatus.HttpStatusCode}\".", results.ConnectionStatus.OriginalException);

            options.HasMore = options.UseLimit && results.Total > options.GetLimit();

            var result = new FindResults<T> {
                Documents = results.Documents.ToList(),
                Total = results.Total
            };

            if (EnableCache && options.UseCache)
                await Cache.SetAsync(GetScopedCacheKey(options.CacheKey), result, options.GetCacheExpirationDate()).AnyContext();

            return result;
        }
        
        protected async Task<T> FindOneAsync(OneOptions options) {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            
            if (EnableCache && options.UseCache) {
                var cacheValue = await Cache.GetAsync<T>(GetScopedCacheKey(options.CacheKey)).AnyContext();
#if DEBUG
                Log.Trace().Message("Cache {0}: type={1}", cacheValue.HasValue ? "hit" : "miss", _entityType).Write();
#endif
                if (cacheValue.HasValue)
                    return cacheValue.Value;
            }

            var searchDescriptor = new SearchDescriptor<T>().Filter(options.GetElasticSearchFilter<T>(_supportsSoftDeletes)).Size(1);
            if (options.Fields.Count > 0)
                searchDescriptor.Source(s => s.Include(options.Fields.ToArray()));
            else
                searchDescriptor.Source(s => s.Exclude("idx"));

            var elasticSearchOptions = options as ElasticSearchOptions<T>;
            searchDescriptor.Indices(elasticSearchOptions != null && elasticSearchOptions.Indices.Any() ? elasticSearchOptions.Indices.ToArray() : GetIndices());

            if (elasticSearchOptions != null && elasticSearchOptions.SortBy.Count > 0) {
                foreach (var sort in elasticSearchOptions.SortBy)
                    searchDescriptor.Sort(sort);
            }

            _elasticClient.EnableTrace();
            var result = (await _elasticClient.SearchAsync<T>(searchDescriptor).AnyContext()).Documents.FirstOrDefault();
            _elasticClient.DisableTrace();
            
            if (EnableCache && options.UseCache && result != null)
                await Cache.SetAsync(GetScopedCacheKey(options.CacheKey), result, options.GetCacheExpirationDate()).AnyContext();

            return result;
        }
        
        public Task<bool> ExistsAsync(string id) {
            if (String.IsNullOrEmpty(id))
                return Task.FromResult(false);

            return ExistsAsync(new OneOptions().WithId(id));
        }

        protected async Task<bool> ExistsAsync(OneOptions options) {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            options.Fields.Add("id");
            var searchDescriptor = new SearchDescriptor<T>().Filter(options.GetElasticSearchFilter<T>(_supportsSoftDeletes)).Size(1);

            var elasticSearchOptions = options as ElasticSearchOptions<T>;
            searchDescriptor.Indices(elasticSearchOptions != null && elasticSearchOptions.Indices.Any()
                ? elasticSearchOptions.Indices.ToArray()
                : GetIndices()); 
            if (elasticSearchOptions != null && elasticSearchOptions.SortBy.Count > 0)
            {
                foreach (var sort in elasticSearchOptions.SortBy)
                    searchDescriptor.Sort(sort);
            }

            return (await _elasticClient.SearchAsync<T>(searchDescriptor).AnyContext()).HitsMetaData.Total > 0;
        }

        protected async Task<long> CountAsync(ElasticSearchOptions<T> options) {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            
            if (EnableCache && options.UseCache) {
                var cachedValue = await Cache.GetAsync<long>(GetScopedCacheKey("count-" + options.CacheKey)).AnyContext();
                if (cachedValue.HasValue)
                    return cachedValue.Value;
            }

            var countDescriptor = new CountDescriptor<T>().Query(f => f.Filtered(s => s.Filter(f2 => options.GetElasticSearchFilter(_supportsSoftDeletes))));
            countDescriptor.Indices(options.Indices.Any()
                ? options.Indices.ToArray()
                : GetIndices());
			
            countDescriptor.IgnoreUnavailable();

            countDescriptor.Type(typeof(T));

            _elasticClient.EnableTrace();
            var results = await _elasticClient.CountAsync<T>(countDescriptor).AnyContext();
            _elasticClient.DisableTrace();

            if (!results.IsValid)
                throw new ApplicationException($"ElasticSearch error code \"{results.ConnectionStatus.HttpStatusCode}\".", results.ConnectionStatus.OriginalException);
            
            if (EnableCache && options.UseCache)
                await Cache.SetAsync(GetScopedCacheKey("count-" + options.CacheKey), results.Count, options.GetCacheExpirationDate()).AnyContext();

            return results.Count;
        }

        protected async Task<IDictionary<string, long>> SimpleAggregationAsync(ElasticSearchOptions<T> options, Expression<Func<T, object>> fieldExpression) {
            var searchDescriptor = new SearchDescriptor<T>()
                .Query(f => f.Filtered(s => s.Filter(f2 => options.GetElasticSearchFilter(_supportsSoftDeletes))))
                .Aggregations(a => a.Terms("simple", sel => sel.Field(fieldExpression).Size(10)));
            
            searchDescriptor.Indices(options.Indices.Any()
                ? options.Indices.ToArray()
                : GetIndices());

            _elasticClient.EnableTrace();
            var aggResults = await _elasticClient.SearchAsync<T>(searchDescriptor).AnyContext();
            _elasticClient.DisableTrace();

            return aggResults.Aggs.Terms("simple").Items.ToDictionary(ar => ar.Key, ar => ar.DocCount);
        }

        public async Task<long> CountAsync() {
            return (await _elasticClient.CountAsync<T>(c => c.Query(q => q.MatchAll()).Indices(GetIndices())).AnyContext()).Count;
        }

        public async Task<T> GetByIdAsync(string id, bool useCache = false, TimeSpan? expiresIn = null) {
            if (String.IsNullOrEmpty(id))
                return null;
            
            if (EnableCache && useCache) {
                var cacheValue = await Cache.GetAsync<T>(GetScopedCacheKey(id)).AnyContext();
#if DEBUG
                Log.Trace().Message("Cache {0}: type={1}", cacheValue.HasValue ? "hit" : "miss", _entityType).Write();
#endif
                if (cacheValue.HasValue)
                    return cacheValue.Value;
            }

            T result = null;
            // try using the object id to figure out what index the entity is located in
            string index = GetIndexName(id);
            if (index != null) {
                _elasticClient.EnableTrace();
                result = (await _elasticClient.GetAsync<T>(f => f.Id(id).Index(index).SourceExclude("idx")).AnyContext()).Source;
                _elasticClient.DisableTrace();
            }

            // TODO:see if we can get rid of this.
            // fallback to doing a find
            if (result == null)
                result = await FindOneAsync(new OneOptions().WithId(id).WithCacheKey(EnableCache && useCache ? id : null).WithExpiresIn(expiresIn)).AnyContext();

            if (EnableCache && useCache && result != null)
                await Cache.SetAsync(GetScopedCacheKey(id), result, expiresIn ?? TimeSpan.FromSeconds(RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS)).AnyContext();

            return result;
        }

        public async Task<FindResults<T>> GetByIdsAsync(ICollection<string> ids, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (ids == null || ids.Count == 0)
                return new FindResults<T>();

            ids = ids.Where(id => !String.IsNullOrEmpty(id)).Distinct().ToList();

            var results = new List<T>();
            if (EnableCache && useCache) {
                foreach (var id in ids) {
                    var cacheHit = await Cache.GetAsync<T>(GetScopedCacheKey(id)).AnyContext();
                    if (cacheHit.HasValue)
                        results.Add(cacheHit.Value);
                }

                var notCachedIds = ids.Except(results.Select(i => i.Id)).ToArray();
                if (notCachedIds.Length == 0)
                    return new FindResults<T> { Documents = results, Total = results.Count };
            }

            // try using the object id to figure out what index the entity is located in
            var foundItems = new List<T>();
            var itemsToFind = new List<string>();
            var multiGet = new MultiGetDescriptor();
			
            // TODO Use the index..
            foreach (var id in ids.Except(results.Select(i => i.Id))) {
                string index = GetIndexName(id);
                if (index != null)
                    multiGet.Get<T>(f => f.Id(id).Index(index).Source(s => s.Exclude("idx")));
                else
                    itemsToFind.Add(id);
            }

            _elasticClient.EnableTrace();
            foreach (var doc in (await _elasticClient.MultiGetAsync(multiGet).AnyContext()).Documents) {
                if (doc.Found)
                    foundItems.Add(doc.Source as T);
                else
                    itemsToFind.Add(doc.Id);
            }
            _elasticClient.DisableTrace();

            // fallback to doing a find
            if (itemsToFind.Count > 0)
                foundItems.AddRange((await FindAsync(new ElasticSearchOptions<T>().WithIds(itemsToFind)).AnyContext()).Documents);

            if (EnableCache && useCache && foundItems.Count > 0) {
                foreach (var item in foundItems)
                    await Cache.SetAsync(GetScopedCacheKey(item.Id), item, expiresIn.HasValue ? DateTime.UtcNow.Add(expiresIn.Value) : DateTime.UtcNow.AddSeconds(RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS)).AnyContext();
            }

            results.AddRange(foundItems);
            return new FindResults<T> {
                Documents = results,
                Total = results.Count
            };
        }

        private string GetIndexName(string id) {
            if (_isEvent) {
                ObjectId objectId;
                if (ObjectId.TryParse(id, out objectId) && objectId.CreationTime > MIN_OBJECTID_DATE)
                    return String.Concat(_index.VersionedName, "-", objectId.CreationTime.ToString("yyyyMM"));

                return null;
            }

            return _index.VersionedName;
        }

        public Task<FindResults<T>> GetAllAsync(string sort = null, SortOrder sortOrder = SortOrder.Ascending, PagingOptions paging = null) {
            var search = new ElasticSearchOptions<T>()
                .WithPaging(paging)
                .WithSort(sort, sortOrder);

            return FindAsync(search);
        }

        public Task<FindResults<T>> GetBySearchAsync(string systemFilter, string userFilter = null, string query = null, string sort = null, SortOrder sortOrder = SortOrder.Ascending, PagingOptions paging = null) {
            var search = new ElasticSearchOptions<T>()
                .WithSystemFilter(systemFilter)
                .WithFilter(userFilter)
                .WithQuery(query, false)
                .WithSort(sort, sortOrder)
                .WithPaging(paging);

            return FindAsync(search);
        }
    }
}