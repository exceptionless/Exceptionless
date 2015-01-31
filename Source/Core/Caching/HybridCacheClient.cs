using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Messaging;
using StackExchange.Redis;

namespace Exceptionless.Core.Caching {
    public class HybridCacheClient : ICacheClient {
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
            _localCache.RemoveAll(message.Keys);
        }

        public bool Remove(string key) {
            _messageBus.Publish(new InvalidateCache { Keys = new[] { key } });
            _localCache.Remove(key);
            return _redisCache.Remove(key);
        }

        public void RemoveAll(IEnumerable<string> keys) {
            _messageBus.Publish(new InvalidateCache { Keys = keys.ToArray() });
            _localCache.RemoveAll(keys);
            _redisCache.RemoveAll(keys);
        }

        public T Get<T>(string key) {
            T value;
            if (_localCache.TryGet(key, out value))
                return value;

            if (_redisCache.TryGet(key, out value)) {
                // TODO: Get the expiration value and set it here.
                _localCache.Set(key, value);
            }

            return value;
        }

        public bool TryGet<T>(string key, out T value) {
            if (_localCache.TryGet(key, out value))
                return true;

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
            _messageBus.Publish(new InvalidateCache { Keys = new [] { key } });
            _localCache.Set(key, value);
            return _redisCache.Set(key, value);
        }

        public bool Set<T>(string key, T value, DateTime expiresAt) {
            _messageBus.Publish(new InvalidateCache { Keys = new[] { key } });
            _localCache.Set(key, value, expiresAt);
            return _redisCache.Set(key, value, expiresAt);
        }

        public bool Set<T>(string key, T value, TimeSpan expiresIn) {
            _messageBus.Publish(new InvalidateCache { Keys = new[] { key } });
            _localCache.Set(key, value, expiresIn);
            return _redisCache.Set(key, value, expiresIn);
        }

        public bool Replace<T>(string key, T value) {
            _messageBus.Publish(new InvalidateCache { Keys = new[] { key } });
            _localCache.Replace(key, value);
            return _redisCache.Replace(key, value);
        }

        public bool Replace<T>(string key, T value, DateTime expiresAt) {
            _messageBus.Publish(new InvalidateCache { Keys = new[] { key } });
            _localCache.Set(key, value, expiresAt);
            return _redisCache.Set(key, value, expiresAt);
        }

        public bool Replace<T>(string key, T value, TimeSpan expiresIn) {
            _messageBus.Publish(new InvalidateCache { Keys = new[] { key } });
            _localCache.Set(key, value, expiresIn);
            return _redisCache.Set(key, value, expiresIn);
        }

        public void FlushAll() {
            _messageBus.Publish(new InvalidateCache { FlushAll = true });
            _localCache.FlushAll();
            _redisCache.FlushAll();
        }

        public IDictionary<string, T> GetAll<T>(IEnumerable<string> keys) {
            return _redisCache.GetAll<T>(keys);
        }

        public void SetAll<T>(IDictionary<string, T> values) {
            if (values != null)
                _messageBus.Publish(new InvalidateCache { Keys = values.Keys.ToArray() });
            _redisCache.SetAll(values);
        }

        public void SetExpiration(string cacheKey, TimeSpan expiresIn) {
            _messageBus.Publish(new InvalidateCache { Keys = new[] { cacheKey } });
            _localCache.Remove(cacheKey);
            _redisCache.SetExpiration(cacheKey, expiresIn);
        }

        public void SetExpiration(string cacheKey, DateTime expiresAt) {
            _messageBus.Publish(new InvalidateCache { Keys = new[] { cacheKey } });
            _localCache.Remove(cacheKey);
            _redisCache.SetExpiration(cacheKey, expiresAt);
        }

        public void Dispose() {}

        public class InvalidateCache {
            public string[] Keys { get; set; }
            public bool FlushAll { get; set; }
        }
    }
}
