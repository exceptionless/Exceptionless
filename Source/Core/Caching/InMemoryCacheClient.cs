using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NLog.Fluent;

namespace Exceptionless.Core.Caching {
    public class InMemoryCacheClient : ICacheClient {
        private ConcurrentDictionary<string, CacheEntry> _memory;

        public InMemoryCacheClient() {
            _memory = new ConcurrentDictionary<string, CacheEntry>();
        }

        public bool FlushOnDispose { get; set; }
        public int Count { get { return _memory.Count; } }
        public int? MaxItems { get; set; }

        internal ICollection<string> Keys {
            get { return _memory.OrderBy(kvp => kvp.Value.LastAccessTicks).ThenBy(kvp => kvp.Value.InstanceNumber).Select(kvp => kvp.Key).ToList(); }
        } 

        private bool CacheAdd(string key, object value) {
            return CacheAdd(key, value, DateTime.MaxValue);
        }

        private bool TryGetValue(string key, out CacheEntry entry) {
            return _memory.TryGetValue(key, out entry);
        }

        private void Set(string key, CacheEntry entry) {
            _memory[key] = entry;

            if (MaxItems.HasValue && _memory.Count > MaxItems.Value) {
                string oldest = _memory.OrderBy(kvp => kvp.Value.LastAccessTicks).ThenBy(kvp => kvp.Value.InstanceNumber).First().Key;
                CacheEntry cacheEntry;
                _memory.TryRemove(oldest, out cacheEntry);
            }
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
            expiresAt = expiresAt.ToUniversalTime();
            if (expiresAt < DateTime.UtcNow)
                throw new ArgumentException("Date cannot be in the past", "expiresAt");

            return CacheSet(key, value, expiresAt, null);
        }

        private bool CacheSet(string key, object value, DateTime expiresAt, long? checkLastModified) {
            expiresAt = expiresAt.ToUniversalTime();
            if (expiresAt < DateTime.UtcNow)
                throw new ArgumentException("Date cannot be in the past", "expiresAt");

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
            expiresAt = expiresAt.ToUniversalTime();
            if (expiresAt < DateTime.UtcNow)
                throw new ArgumentException("Date cannot be in the past", "expiresAt");

            return !CacheSet(key, value, expiresAt);
        }

        public void Dispose() {
            if (!FlushOnDispose) return;

            _memory = new ConcurrentDictionary<string, CacheEntry>();
        }

        public bool Remove(string key) {
            if (String.IsNullOrEmpty(key))
                return true;

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

        public bool TryGet<T>(string key, out T value) {
            value = default(T);
            if (!_memory.ContainsKey(key))
                return false;

            try {
                value = Get<T>(key);
                return true;
            } catch {
                return false;
            }
        }

        private readonly object _lockObject = new object();
        private long UpdateCounter(string key, long value, TimeSpan? expiresIn = null) {
            lock (_lockObject) {
                if (!_memory.ContainsKey(key)) {
                    if (expiresIn.HasValue)
                        Set(key, value, expiresIn.Value);
                    else
                        Set(key, value);
                    return value;
                }

                var current = Get<long>(key);
                if (expiresIn.HasValue)
                    Set(key, current += value, expiresIn.Value);
                else
                    Set(key, current += value);
                return current;
            }
        }

        public long Increment(string key, uint amount) {
            return UpdateCounter(key, amount);
        }

        public long Increment(string key, uint amount, DateTime expiresAt) {
            var expires = expiresAt.ToUniversalTime().Subtract(DateTime.UtcNow);
            if (expires.Ticks < 0)
                throw new ArgumentException("Date cannot be in the past", "expiresAt");

            return UpdateCounter(key, amount, expires);
        }

        public long Increment(string key, uint amount, TimeSpan expiresIn) {
            return UpdateCounter(key, amount, expiresIn);
        }

        public long Decrement(string key, uint amount) {
            return UpdateCounter(key, amount * -1);
        }

        public long Decrement(string key, uint amount, DateTime expiresAt) {
            var expires = expiresAt.ToUniversalTime().Subtract(DateTime.UtcNow);
            if (expires.Ticks < 0)
                throw new ArgumentException("Date cannot be in the past", "expiresAt");

            return UpdateCounter(key, amount * -1, expires);
        }

        public long Decrement(string key, uint amount, TimeSpan expiresIn) {
            return UpdateCounter(key, amount * -1, expiresIn);
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
            expiresAt = expiresAt.ToUniversalTime();
            if (expiresAt < DateTime.UtcNow)
                throw new ArgumentException("Date cannot be in the past", "expiresAt");

            return CacheAdd(key, value, expiresAt);
        }

        public bool Set<T>(string key, T value, DateTime expiresAt) {
            expiresAt = expiresAt.ToUniversalTime();
            if (expiresAt < DateTime.UtcNow)
                throw new ArgumentException("Date cannot be in the past", "expiresAt");

            return CacheSet(key, value, expiresAt);
        }

        public bool Replace<T>(string key, T value, DateTime expiresAt) {
            expiresAt = expiresAt.ToUniversalTime();
            if (expiresAt < DateTime.UtcNow)
                throw new ArgumentException("Date cannot be in the past", "expiresAt");

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
            _memory.Clear();
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

        public DateTime? GetExpiration(string cacheKey) {
            CacheEntry value;
            if (!_memory.TryGetValue(cacheKey, out value))
                return null;

            if (value.ExpiresAt >= DateTime.UtcNow)
                return value.ExpiresAt;

            _memory.TryRemove(cacheKey, out value);
            return null;
        }

        public void SetExpiration(string cacheKey, TimeSpan expiresIn) {
            CacheEntry value;
            if (_memory.TryGetValue(cacheKey, out value))
                value.ExpiresAt = DateTime.UtcNow.Add(expiresIn);
        }

        public void SetExpiration(string cacheKey, DateTime expiresAt) {
            expiresAt = expiresAt.ToUniversalTime();
            if (expiresAt < DateTime.UtcNow)
                throw new ArgumentException("Date cannot be in the past", "expiresAt");

            CacheEntry value;
            if (_memory.TryGetValue(cacheKey, out value))
                value.ExpiresAt = expiresAt;
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

        private class CacheEntry {
            private object _cacheValue;
            private static long _instanceCount = 0;
#if DEBUG
            private long _usageCount = 0;
#endif

            public CacheEntry(object value, DateTime expiresAt) {
                Value = value;
                ExpiresAt = expiresAt;
                LastModifiedTicks = DateTime.UtcNow.Ticks;
                InstanceNumber = Interlocked.Increment(ref _instanceCount);
            }

            internal long InstanceNumber { get; private set; }
            internal DateTime ExpiresAt { get; set; }
            internal long LastAccessTicks { get; private set; }
            internal long LastModifiedTicks { get; private set; }
#if DEBUG
            internal long UsageCount { get { return _usageCount; } }
#endif

            internal object Value {
                get {
                    LastAccessTicks = DateTime.UtcNow.Ticks;
#if DEBUG
                    Interlocked.Increment(ref _usageCount);
#endif
                    return _cacheValue;
                }
                set {
                    _cacheValue = value;
                    LastAccessTicks = DateTime.UtcNow.Ticks;
                    LastModifiedTicks = DateTime.UtcNow.Ticks;
                }
            }
        }
    }
}