﻿using System;
using System.Collections.Generic;
﻿using Microsoft.ApplicationServer.Caching;
﻿using NLog.Fluent;

namespace Exceptionless.Core.Caching {
    public class AzureCacheClient : ICacheClient {
        private DataCacheFactory CacheFactory { get; set; }
        private DataCache DataCache { get; set; }
        public bool FlushOnDispose { get; set; }

        public AzureCacheClient(string cacheName = null) {
            CacheFactory = new DataCacheFactory();
            if (string.IsNullOrEmpty(cacheName))
                DataCache = CacheFactory.GetDefaultCache();
            else
                DataCache = CacheFactory.GetCache(cacheName);
        }

        private bool TryGetValue(string key, out object entry) {
            entry = DataCache.Get(key);
            return entry != null;
        }

        private bool CacheAdd(string key, object value) {
            return CacheAdd(key, value, DateTime.MaxValue);
        }

        private bool CacheAdd(string key, object value, DateTime expiresAt) {
            object entry;
            if (TryGetValue(key, out entry))
                return false;
            DataCache.Add(key, value, expiresAt.Subtract(DateTime.Now));
            return true;
        }

        private bool CacheSet(string key, object value) {
            return CacheSet(key, value, DateTime.MaxValue);
        }

        private bool CacheSet(string key, object value, DateTime expiresAt, DataCacheItemVersion checkLastVersion = null) {
            if (checkLastVersion != null) {
                object entry = DataCache.GetIfNewer(key, ref checkLastVersion);
                if (entry != null) {
                    // update value and version
                    DataCache.Put(key, value, checkLastVersion, expiresAt.Subtract(DateTime.Now));
                    return true;
                }
                if (TryGetValue(key, out entry)) {
                    // version exists but is older.
                    return false;
                }
            }

            // if we don't care about version, then just update
            DataCache.Put(key, value, expiresAt.Subtract(DateTime.Now));
            return true;
        }

        private bool CacheReplace(string key, object value) {
            return CacheReplace(key, value, DateTime.MaxValue);
        }

        private bool CacheReplace(string key, object value, DateTime expiresAt) {
            return !CacheSet(key, value, expiresAt); ;
        }

        public void Dispose() {
            if (!FlushOnDispose) return;

            FlushAll();
        }

        public bool Remove(string key) {
            return DataCache.Remove(key);
        }

        public void RemoveAll(IEnumerable<string> keys) {
            foreach (var key in keys) {
                try {
                    Remove(key);
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Error trying to remove {0} from azure cache", key).Write();
                }
            }
        }

        public object Get(string key) {
            DataCacheItemVersion version;
            return Get(key, out version);
        }

        public object Get(string key, out DataCacheItemVersion version) {
            return DataCache.Get(key, out version);
        }

        public T Get<T>(string key) {
            var value = Get(key);
            if (value != null) return (T)value;
            return default(T);
        }

        public long Increment(string key, uint amount) {
            return UpdateCounter(key, (int)amount);
        }

        private long UpdateCounter(string key, int value) {
            long longVal;
            if (Int64.TryParse(Get(key).ToString(), out longVal)) {
                longVal += value;
                CacheSet(key, longVal);
                return longVal;
            }
            CacheSet(key, 0);
            return 0;
        }

        public long Decrement(string key, uint amount) {
            return UpdateCounter(key, (int)-amount);
        }

        public bool Add<T>(string key, T value) {
            return CacheAdd(key, value);
        }

        public bool Set<T>(string key, T value) {
            return CacheSet(key, value);
        }

        public bool Replace<T>(string key, T value) {
            return CacheReplace(key, value);
        }

        public bool Add<T>(string key, T value, DateTime expiresAt) {
            return CacheAdd(key, value, expiresAt);
        }

        public bool Set<T>(string key, T value, DateTime expiresAt) {
            return CacheSet(key, value, expiresAt);
        }

        public bool Replace<T>(string key, T value, DateTime expiresAt) {
            return CacheReplace(key, value, expiresAt);
        }

        public bool Add<T>(string key, T value, TimeSpan expiresIn) {
            return CacheAdd(key, value, DateTime.Now.Add(expiresIn));
        }

        public bool Set<T>(string key, T value, TimeSpan expiresIn) {
            return CacheSet(key, value, DateTime.Now.Add(expiresIn));
        }

        public bool Replace<T>(string key, T value, TimeSpan expiresIn) {
            return CacheReplace(key, value, DateTime.Now.Add(expiresIn));
        }

        public void FlushAll() {
            var regions = DataCache.GetSystemRegions();
            foreach (var region in regions) {
                DataCache.ClearRegion(region);
            }
        }

        public IDictionary<string, T> GetAll<T>(IEnumerable<string> keys) {
            var valueMap = new Dictionary<string, T>();
            foreach (var key in keys) {
                var value = Get<T>(key);
                valueMap[key] = value;
            }
            return valueMap;
        }

        public void SetAll<T>(IDictionary<string, T> values) {
            foreach (var entry in values) {
                Set(entry.Key, entry.Value);
            }
        }

        public void SetExpiration(string cacheKey, TimeSpan expiresIn) {
            SetExpiration(cacheKey, DateTime.Now.Add(expiresIn));
        }

        public void SetExpiration(string cacheKey, DateTime expiresAt) {
            object value;
            if (!TryGetValue(cacheKey, out value))
                throw new ArgumentException();

            CacheReplace(cacheKey, value, expiresAt);
        }
    }
}
