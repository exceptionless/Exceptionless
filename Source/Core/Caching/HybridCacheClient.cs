using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Exceptionless.Core.Messaging;
using NLog.Fluent;
using StackExchange.Redis;

namespace Exceptionless.Core.Caching {
    public class HybridCacheClient : ICacheClient {
        private readonly string _cacheId = Guid.NewGuid().ToString("N");
        private readonly ICacheClient _redisCache;
        private readonly InMemoryCacheClient _localCache = new InMemoryCacheClient();
        private readonly RedisMessageBus _messageBus;

        public HybridCacheClient(ConnectionMultiplexer connectionMultiplexer) {
            _redisCache = new RedisCacheClient(connectionMultiplexer);
            _localCache.MaxItems = 100;
            _messageBus = new RedisMessageBus(connectionMultiplexer.GetSubscriber(), "cache");
            _messageBus.Subscribe<InvalidateCache>(OnMessage);
        }

        internal InMemoryCacheClient LocalCache { get { return _localCache; } }

        public int LocalCacheSize {
            get { return _localCache.MaxItems ?? -1; }
            set { _localCache.MaxItems = value; }
        }

        private void OnMessage(InvalidateCache message) {
            if (!String.IsNullOrEmpty(message.CacheId) && String.Equals(_cacheId, message.CacheId))
                return;

            Log.Trace().Message("Invalidating local cache from remote: id={0} keys={1}", message.CacheId, String.Join(",", message.Keys ?? new string[] {})).Write();
            if (message.FlushAll)
                _localCache.FlushAll();
            else if (message.Keys != null && message.Keys.Length > 0)
                _localCache.RemoveAll(message.Keys);
            else
                Log.Warn().Message("Unknown invalidate cache message").Write();
        }

        public bool Remove(string key) {
            if (String.IsNullOrEmpty(key))
                return true;

            _messageBus.Publish(new InvalidateCache { CacheId = _cacheId, Keys = new[] { key } });
            _localCache.Remove(key);
            return _redisCache.Remove(key);
        }

        public void RemoveAll(IEnumerable<string> keys) {
            if (keys == null)
                return;

            var keysToRemove = keys.ToArray();
            if (keysToRemove.Length == 0)
                return;

            _messageBus.Publish(new InvalidateCache { CacheId = _cacheId, Keys = keysToRemove });
            _localCache.RemoveAll(keysToRemove);
            _redisCache.RemoveAll(keysToRemove);
        }

        public T Get<T>(string key) {
            T value;
            if (_localCache.TryGet(key, out value)) {
                Log.Trace().Message("Local cache hit: {0}", key).Write();
                return value;
            }

            if (_redisCache.TryGet(key, out value)) {
                var expiration = _redisCache.GetExpiration(key);
                if (expiration.HasValue)
                    _localCache.Set(key, value, expiration.Value);
                else
                    _localCache.Set(key, value);
            }

            return value;
        }

        public DateTime? GetExpiration(string key) {
            var expiration = _redisCache.GetExpiration(key);
            if (expiration.HasValue)
                return expiration.Value;

            return _redisCache.GetExpiration(key);
        }

        public bool TryGet<T>(string key, out T value) {
            if (_localCache.TryGet(key, out value)) {
                Log.Trace().Message("Local cache hit: {0}", key).Write();
                return true;
            }

            if (_redisCache.TryGet(key, out value)) {
                _localCache.Set(key, value);
                return true;
            }

            return false;
        }

        public long Increment(string key, uint amount) {
            return _redisCache.Increment(key, amount);
        }

        public long Increment(string key, uint amount, DateTime expiresAt) {
            return _redisCache.Increment(key, amount, expiresAt);
        }

        public long Increment(string key, uint amount, TimeSpan expiresIn) {
            return _redisCache.Increment(key, amount, expiresIn);
        }

        public long Decrement(string key, uint amount) {
            return _redisCache.Decrement(key, amount);
        }

        public long Decrement(string key, uint amount, DateTime expiresAt) {
            return _redisCache.Decrement(key, amount, expiresAt);
        }

        public long Decrement(string key, uint amount, TimeSpan expiresIn) {
            return _redisCache.Decrement(key, amount, expiresIn);
        }

        public bool Add<T>(string key, T value) {
            _localCache.Add(key, value);
            return _redisCache.Add(key, value);
        }

        public bool Add<T>(string key, T value, DateTime expiresAt) {
            _localCache.Add(key, value, expiresAt);
            return _redisCache.Add(key, value, expiresAt);
        }

        public bool Add<T>(string key, T value, TimeSpan expiresIn) {
            _localCache.Add(key, value, expiresIn);
            return _redisCache.Add(key, value, expiresIn);
        }

        public bool Set<T>(string key, T value) {
            _messageBus.Publish(new InvalidateCache { CacheId = _cacheId, Keys = new [] { key } });
            _localCache.Set(key, value);
            return _redisCache.Set(key, value);
        }

        public bool Set<T>(string key, T value, DateTime expiresAt) {
            _messageBus.Publish(new InvalidateCache { CacheId = _cacheId, Keys = new[] { key } });
            _localCache.Set(key, value, expiresAt);
            return _redisCache.Set(key, value, expiresAt);
        }

        public bool Set<T>(string key, T value, TimeSpan expiresIn) {
            _messageBus.Publish(new InvalidateCache { CacheId = _cacheId, Keys = new[] { key } });
            _localCache.Set(key, value, expiresIn);
            return _redisCache.Set(key, value, expiresIn);
        }

        public bool Replace<T>(string key, T value) {
            _messageBus.Publish(new InvalidateCache { CacheId = _cacheId, Keys = new[] { key } });
            _localCache.Replace(key, value);
            return _redisCache.Replace(key, value);
        }

        public bool Replace<T>(string key, T value, DateTime expiresAt) {
            _messageBus.Publish(new InvalidateCache { CacheId = _cacheId, Keys = new[] { key } });
            _localCache.Set(key, value, expiresAt);
            return _redisCache.Set(key, value, expiresAt);
        }

        public bool Replace<T>(string key, T value, TimeSpan expiresIn) {
            _messageBus.Publish(new InvalidateCache { CacheId = _cacheId, Keys = new[] { key } });
            _localCache.Set(key, value, expiresIn);
            return _redisCache.Set(key, value, expiresIn);
        }

        public void FlushAll() {
            _messageBus.Publish(new InvalidateCache { CacheId = _cacheId, FlushAll = true });
            _localCache.FlushAll();
            _redisCache.FlushAll();
        }

        public IDictionary<string, T> GetAll<T>(IEnumerable<string> keys) {
            return _redisCache.GetAll<T>(keys);
        }

        public void SetAll<T>(IDictionary<string, T> values) {
            if (values == null)
                return;
            
            _messageBus.Publish(new InvalidateCache { CacheId = _cacheId, Keys = values.Keys.ToArray() });
            _localCache.SetAll(values);
            _redisCache.SetAll(values);
        }

        public void SetExpiration(string key, TimeSpan expiresIn) {
            _messageBus.Publish(new InvalidateCache { CacheId = _cacheId, Keys = new[] { key } });
            _localCache.Remove(key);
            _redisCache.SetExpiration(key, expiresIn);
        }

        public void SetExpiration(string key, DateTime expiresAt) {
            _messageBus.Publish(new InvalidateCache { CacheId = _cacheId, Keys = new[] { key } });
            _localCache.Remove(key);
            _redisCache.SetExpiration(key, expiresAt);
        }

        public void Dispose() {}

        public class InvalidateCache {
            public string CacheId { get; set; }
            public string[] Keys { get; set; }
            public bool FlushAll { get; set; }
        }
    }
}
