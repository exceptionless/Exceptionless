using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Exceptionless.Core.Caching {
    public class RedisCacheClient : ICacheClient {
        private readonly ConnectionMultiplexer _connectionMultiplexer;
        private readonly IDatabase _db;

        public RedisCacheClient(ConnectionMultiplexer connectionMultiplexer) {
            _connectionMultiplexer = connectionMultiplexer;
            _db = connectionMultiplexer.GetDatabase();
        }

        public bool Remove(string key) {
            if (String.IsNullOrEmpty(key))
                return true;

            return _db.KeyDelete(key);
        }

        public void RemoveAll(IEnumerable<string> keys) {
            var redisKeys = keys.Where(k => !String.IsNullOrEmpty(k)).Select(k => (RedisKey)k).ToArray();
            if (redisKeys.Length > 0)
                _db.KeyDelete(redisKeys);
        }

        public T Get<T>(string key) {
            var value = _db.StringGet(key);
            if (value.IsNullOrEmpty)
                return default(T);

            return JsonConvert.DeserializeObject<T>(value.ToString());
        }

        public long Increment(string key, uint amount) {
            return _db.StringIncrement(key, amount);
        }

        public long Increment(string key, uint amount, DateTime expiresAt) {
            var result = _db.StringIncrement(key, amount);
            _db.KeyExpire(key, expiresAt);
            return result;
        }

        public long Increment(string key, uint amount, TimeSpan expiresIn) {
            var result = _db.StringIncrement(key, amount);
            _db.KeyExpire(key, expiresIn);
            return result;
        }

        public long Decrement(string key, uint amount) {
            return _db.StringDecrement(key, amount);
        }

        public long Decrement(string key, uint amount, DateTime expiresAt) {
            var result = _db.StringDecrement(key, amount);
            _db.KeyExpire(key, expiresAt);
            return result;
        }

        public long Decrement(string key, uint amount, TimeSpan expiresIn) {
            var result = _db.StringDecrement(key, amount);
            _db.KeyExpire(key, expiresIn);
            return result;
        }

        public bool Add<T>(string key, T value) {
            var json = JsonConvert.SerializeObject(value);
            return _db.StringSet(key, json, null, When.NotExists);
        }

        public bool Set<T>(string key, T value) {
            var json = JsonConvert.SerializeObject(value);
            return _db.StringSet(key, json);
        }

        public bool Replace<T>(string key, T value) {
            var json = JsonConvert.SerializeObject(value);
            return _db.StringSet(key, json, null, When.Exists);
        }

        public bool Add<T>(string key, T value, DateTime expiresAt) {
            var json = JsonConvert.SerializeObject(value);
            return _db.StringSet(key, json, expiresAt.Subtract(DateTime.Now), When.NotExists);
        }

        public bool Set<T>(string key, T value, DateTime expiresAt) {
            var json = JsonConvert.SerializeObject(value);
            return _db.StringSet(key, json, expiresAt.Subtract(DateTime.Now));
        }

        public bool Replace<T>(string key, T value, DateTime expiresAt) {
            var json = JsonConvert.SerializeObject(value);
            return _db.StringSet(key, json, expiresAt.Subtract(DateTime.Now), When.Exists);
        }

        public bool Add<T>(string key, T value, TimeSpan expiresIn) {
            var json = JsonConvert.SerializeObject(value);
            return _db.StringSet(key, json, expiresIn, When.NotExists);
        }

        public bool Set<T>(string key, T value, TimeSpan expiresIn) {
            var json = JsonConvert.SerializeObject(value);
            return _db.StringSet(key, json, expiresIn);
        }

        public bool Replace<T>(string key, T value, TimeSpan expiresIn) {
            var json = JsonConvert.SerializeObject(value);
            return _db.StringSet(key, json, expiresIn, When.Exists);
        }

        public void FlushAll() {
            var endpoints = _connectionMultiplexer.GetEndPoints(true);
            if (endpoints.Length == 0)
                return;

            try {
                foreach (var endpoint in endpoints) {
                    var server = _connectionMultiplexer.GetServer(endpoint);
                    server.FlushAllDatabases();
                }
            } catch (Exception) {
                var server = _connectionMultiplexer.GetServer(endpoints.First());
                _db.KeyDelete(server.Keys().ToArray());
            };
        }

        public IDictionary<string, T> GetAll<T>(IEnumerable<string> keys) {
            var keyArray = keys.ToArray();
            var values = _db.StringGet(keyArray.Select(k => (RedisKey)k).ToArray());

            var result = new Dictionary<string, T>();
            for (int i = 0; i < keyArray.Length; i++) {
                T value = JsonConvert.DeserializeObject<T>(values[i]);
                result.Add(keyArray[i], value);
            }

            return result;
        }

        public void SetAll<T>(IDictionary<string, T> values) {
            _db.StringSet(values.ToDictionary(v => (RedisKey)v.Key, v => (RedisValue)JsonConvert.SerializeObject(v.Value)).ToArray());
        }

        public void SetExpiration(string cacheKey, TimeSpan expiresIn) {
            _db.KeyExpire(cacheKey, expiresIn);
        }

        public void SetExpiration(string cacheKey, DateTime expiresAt) {
            _db.KeyExpire(cacheKey, expiresAt);
        }

        public void Dispose() { }
    }
}
