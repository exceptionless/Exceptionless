using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Exceptionless.Core.Caching {
    public class RedisCacheClient : ICacheClient {
        private readonly IDatabase _db;

        public RedisCacheClient(IDatabase db) {
            _db = db;
        }

        public bool Remove(string key) {
            return _db.KeyDelete(key);
        }

        public void RemoveAll(IEnumerable<string> keys) {
            _db.KeyDelete(keys.Select(k => (RedisKey)k).ToArray());
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

        public long Decrement(string key, uint amount) {
            return _db.StringDecrement(key, amount);
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
            throw new NotImplementedException();
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
