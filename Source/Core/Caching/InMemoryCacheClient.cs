using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NLog.Fluent;

namespace Exceptionless.Core.Caching {
    public class InMemoryCacheClient : ICacheClient {
        private ConcurrentDictionary<string, CacheEntry> _memory;

        public bool FlushOnDispose { get; set; }

        private class CacheEntry {
            private object _cacheValue;

            public CacheEntry(object value, DateTime expiresAt) {
                Value = value;
                ExpiresAt = expiresAt;
                LastModifiedTicks = DateTime.UtcNow.Ticks;
            }

            internal DateTime ExpiresAt { get; set; }

            internal object Value {
                get { return _cacheValue; }
                set {
                    _cacheValue = value;
                    LastModifiedTicks = DateTime.UtcNow.Ticks;
                }
            }

            internal long LastModifiedTicks { get; private set; }
        }

        public InMemoryCacheClient() {
            _memory = new ConcurrentDictionary<string, CacheEntry>();
        }

        private bool CacheAdd(string key, object value) {
            return CacheAdd(key, value, DateTime.MaxValue);
        }

        private bool TryGetValue(string key, out CacheEntry entry) {
            return _memory.TryGetValue(key, out entry);
        }

        private void Set(string key, CacheEntry entry) {
            _memory[key] = entry;
        }

        private bool CacheAdd(string key, object value, DateTime expiresAt) {
            CacheEntry entry;
            if (TryGetValue(key, out entry))
                return false;

            entry = new CacheEntry(value, expiresAt);
            Set(key, entry);

            return true;
        }

        private bool CacheSet(string key, object value) {
            return CacheSet(key, value, DateTime.MaxValue);
        }

        private bool CacheSet(string key, object value, DateTime expiresAt) {
            return CacheSet(key, value, expiresAt, null);
        }

        private bool CacheSet(string key, object value, DateTime expiresAt, long? checkLastModified) {
            CacheEntry entry;
            if (!TryGetValue(key, out entry)) {
                entry = new CacheEntry(value, expiresAt);
                Set(key, entry);
                return true;
            }

            if (checkLastModified.HasValue
                && entry.LastModifiedTicks != checkLastModified.Value)
                return false;

            entry.Value = value;
            entry.ExpiresAt = expiresAt;

            return true;
        }

        private bool CacheReplace(string key, object value) {
            return CacheReplace(key, value, DateTime.MaxValue);
        }

        private bool CacheReplace(string key, object value, DateTime expiresAt) {
            return !CacheSet(key, value, expiresAt);
        }

        public void Dispose() {
            if (!FlushOnDispose) return;

            _memory = new ConcurrentDictionary<string, CacheEntry>();
        }

        public bool Remove(string key) {
            CacheEntry item;
            return _memory.TryRemove(key, out item);
        }

        public void RemoveAll(IEnumerable<string> keys) {
            foreach (var key in keys) {
                try {
                    Remove(key);
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Error trying to remove {0} from the cache", key).Write();
                }
            }
        }

        public object Get(string key) {
            long lastModifiedTicks;
            return Get(key, out lastModifiedTicks);
        }

        public object Get(string key, out long lastModifiedTicks) {
            lastModifiedTicks = 0;

            CacheEntry cacheEntry;
            if (!_memory.TryGetValue(key, out cacheEntry))
                return null;

            if (cacheEntry.ExpiresAt < DateTime.UtcNow) {
                _memory.TryRemove(key, out cacheEntry);
                return null;
            }

            lastModifiedTicks = cacheEntry.LastModifiedTicks;
            return cacheEntry.Value;
        }

        public T Get<T>(string key) {
            var value = Get(key);
            if (value != null) return (T)value;
            return default(T);
        }

        private static readonly object _lockObject = new object();
        private long UpdateCounter(string key, long value) {
            lock (_lockObject) {
                if (!_memory.ContainsKey(key)) {
                    Set(key, value);
                    return value;
                }

                var current = Get<long>(key);
                Set(key, current += value);
                return current;
            }
        }

        public long Increment(string key, uint amount) {
            return UpdateCounter(key, amount);
        }

        public long Decrement(string key, uint amount) {
            return UpdateCounter(key, amount * -1);
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
            if (expiresAt.Kind != DateTimeKind.Utc)
                expiresAt = expiresAt.ToUniversalTime();

            return CacheAdd(key, value, expiresAt);
        }

        public bool Set<T>(string key, T value, DateTime expiresAt) {
            if (expiresAt.Kind != DateTimeKind.Utc)
                expiresAt = expiresAt.ToUniversalTime();

            return CacheSet(key, value, expiresAt);
        }

        public bool Replace<T>(string key, T value, DateTime expiresAt) {
            if (expiresAt.Kind != DateTimeKind.Utc)
                expiresAt = expiresAt.ToUniversalTime();

            return CacheReplace(key, value, expiresAt);
        }

        public bool Add<T>(string key, T value, TimeSpan expiresIn) {
            return CacheAdd(key, value, DateTime.UtcNow.Add(expiresIn));
        }

        public bool Set<T>(string key, T value, TimeSpan expiresIn) {
            return CacheSet(key, value, DateTime.UtcNow.Add(expiresIn));
        }

        public bool Replace<T>(string key, T value, TimeSpan expiresIn) {
            return CacheReplace(key, value, DateTime.UtcNow.Add(expiresIn));
        }

        public void FlushAll() {
            _memory = new ConcurrentDictionary<string, CacheEntry>();
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
            foreach (var entry in values)
                Set(entry.Key, entry.Value);
        }

        public void SetExpiration(string cacheKey, TimeSpan expiresIn) {
            if (_memory.ContainsKey(cacheKey))
                _memory[cacheKey].ExpiresAt = DateTime.UtcNow.Add(expiresIn);
        }

        public void SetExpiration(string cacheKey, DateTime expiresAt) {
            if (expiresAt.Kind != DateTimeKind.Utc)
                expiresAt = expiresAt.ToUniversalTime();

            if (_memory.ContainsKey(cacheKey))
                _memory[cacheKey].ExpiresAt = expiresAt;
        }

        public void RemoveByPattern(string pattern) {
            RemoveByRegex(pattern.Replace("*", ".*").Replace("?", ".+"));
        }

        public void RemoveByRegex(string pattern) {
            var regex = new Regex(pattern);
            var enumerator = _memory.GetEnumerator();
            try {
                while (enumerator.MoveNext()) {
                    var current = enumerator.Current;
                    if (regex.IsMatch(current.Key))
                        Remove(current.Key);
                }
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error trying to remove items from cache with this {0} pattern", pattern).Write();
            }
        }

        public int Count { get { return _memory.Count; } }
    }
}