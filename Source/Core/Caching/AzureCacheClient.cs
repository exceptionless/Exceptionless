using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Caching {
    public class AzureCacheClient : ICacheClient {
        public void Dispose() {
            throw new NotImplementedException();
        }

        public bool Remove(string key) {
            throw new NotImplementedException();
        }

        public void RemoveAll(IEnumerable<string> keys) {
            throw new NotImplementedException();
        }

        public T Get<T>(string key) {
            throw new NotImplementedException();
        }

        public long Increment(string key, uint amount) {
            throw new NotImplementedException();
        }

        public long Decrement(string key, uint amount) {
            throw new NotImplementedException();
        }

        public bool Add<T>(string key, T value) {
            throw new NotImplementedException();
        }

        public bool Set<T>(string key, T value) {
            throw new NotImplementedException();
        }

        public bool Replace<T>(string key, T value) {
            throw new NotImplementedException();
        }

        public bool Add<T>(string key, T value, DateTime expiresAt) {
            throw new NotImplementedException();
        }

        public bool Set<T>(string key, T value, DateTime expiresAt) {
            throw new NotImplementedException();
        }

        public bool Replace<T>(string key, T value, DateTime expiresAt) {
            throw new NotImplementedException();
        }

        public bool Add<T>(string key, T value, TimeSpan expiresIn) {
            throw new NotImplementedException();
        }

        public bool Set<T>(string key, T value, TimeSpan expiresIn) {
            throw new NotImplementedException();
        }

        public bool Replace<T>(string key, T value, TimeSpan expiresIn) {
            throw new NotImplementedException();
        }

        public void FlushAll() {
            throw new NotImplementedException();
        }

        public IDictionary<string, T> GetAll<T>(IEnumerable<string> keys) {
            throw new NotImplementedException();
        }

        public void SetAll<T>(IDictionary<string, T> values) {
            throw new NotImplementedException();
        }

        public void SetExpiration(string cacheKey, TimeSpan expiresIn) {
            throw new NotImplementedException();
        }

        public void SetExpiration(string cacheKey, DateTime expiresAt) {
            throw new NotImplementedException();
        }
    }
}
