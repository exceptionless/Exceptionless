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

        public bool TryGet<T>(string key, out T value) {
            value = default(T);
            try {
                var stringValue = _db.StringGet(key);
                if (stringValue.IsNullOrEmpty)
                    return false;

                value = JsonConvert.DeserializeObject<T>(stringValue.ToString());
                return true;
            } catch {
                return false;
            }
        }

        public long Increment(string key, uint amount) {
            return _db.StringIncrement(key, amount);
        }

        public long Increment(string key, uint amount, DateTime expiresAt) {
            return Increment(key, amount, expiresAt.ToUniversalTime().Subtract(DateTime.UtcNow));
        }

        public long Increment(string key, uint amount, TimeSpan expiresIn) {
            if (expiresIn.Ticks < 0) {
                Remove(key);
                return -1;
            }

            var result = _db.StringIncrement(key, amount);
            _db.KeyExpire(key, expiresIn);
            return result;
        }

        public long Decrement(string key, uint amount) {
            return _db.StringDecrement(key, amount);
        }

        public long Decrement(string key, uint amount, DateTime expiresAt) {
            return Decrement(key, amount, expiresAt.ToUniversalTime().Subtract(DateTime.UtcNow));
        }

        public long Decrement(string key, uint amount, TimeSpan expiresIn) {
            if (expiresIn.Ticks < 0) {
                Remove(key);
                return -1;
            }

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

        public bool Set<T>(string key, T value, DateTime expiresAt) {
            return Set(key, value, expiresAt.ToUniversalTime().Subtract(DateTime.UtcNow));
        }

        public bool Replace<T>(string key, T value, DateTime expiresAt) {
            return Replace(key, value, expiresAt.ToUniversalTime().Subtract(DateTime.UtcNow));
        }

        public bool Add<T>(string key, T value, DateTime expiresAt) {
            return Add(key, value, expiresAt.ToUniversalTime().Subtract(DateTime.UtcNow));
        }

        public bool Add<T>(string key, T value, TimeSpan expiresIn) {
            if (expiresIn.Ticks < 0) {
                Remove(key);
                return false;
            }

            var json = JsonConvert.SerializeObject(value);
            return _db.StringSet(key, json, expiresIn, When.NotExists);
        }

        public bool Set<T>(string key, T value, TimeSpan expiresIn) {
            if (expiresIn.Ticks < 0) {
                Remove(key);
                return false;
            }

            var json = JsonConvert.SerializeObject(value);
            return _db.StringSet(key, json, expiresIn);
        }

        public bool Replace<T>(string key, T value, TimeSpan expiresIn) {
            if (expiresIn.Ticks < 0) {
                Remove(key);
                return false;
            }

            var json = JsonConvert.SerializeObject(value);
            return _db.StringSet(key, json, expiresIn, When.Exists);
        }

        public void FlushAll() {
            var endpoints = _connectionMultiplexer.GetEndPoints(true);
            if (endpoints.Length == 0)
                return;

            foreach (var endpoint in endpoints) {
                var server = _connectionMultiplexer.GetServer(endpoint);
                
                try {
                    server.FlushDatabase();
                    continue;
                } catch (Exception) {}

                try {
                    var keys = server.Keys().ToArray();
                    if (keys.Length > 0)
                        _db.KeyDelete(keys);
                } catch (Exception) {}   
            }
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

        public DateTime? GetExpiration(string key) {
            var expiration = _db.KeyTimeToLive(key);
            if (!expiration.HasValue)
                return null;

            return DateTime.UtcNow.Add(expiration.Value);
        }

        public void SetExpiration(string key, DateTime expiresAt) {
            SetExpiration(key, expiresAt.ToUniversalTime().Subtract(DateTime.UtcNow));
        }

        public void SetExpiration(string key, TimeSpan expiresIn) {
            if (expiresIn.Ticks < 0) {
                Remove(key);
                return;
            }

            _db.KeyExpire(key, expiresIn);
        }

        public void Dispose() { }
    }
}