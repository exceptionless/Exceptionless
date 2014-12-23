using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Caching {
    public interface ICacheClient : IDisposable {
        bool Remove(string key);
        void RemoveAll(IEnumerable<string> keys);
        T Get<T>(string key);
        long Increment(string key, uint amount);
        long Increment(string key, uint amount, DateTime expiresAt);
        long Increment(string key, uint amount, TimeSpan expiresIn);
        long Decrement(string key, uint amount);
        long Decrement(string key, uint amount, DateTime expiresAt);
        long Decrement(string key, uint amount, TimeSpan expiresIn);
        bool Add<T>(string key, T value);
        bool Add<T>(string key, T value, DateTime expiresAt);
        bool Add<T>(string key, T value, TimeSpan expiresIn);
        bool Set<T>(string key, T value);
        bool Set<T>(string key, T value, DateTime expiresAt);
        bool Set<T>(string key, T value, TimeSpan expiresIn);
        bool Replace<T>(string key, T value);
        bool Replace<T>(string key, T value, DateTime expiresAt);
        bool Replace<T>(string key, T value, TimeSpan expiresIn);
        void FlushAll();
        IDictionary<string, T> GetAll<T>(IEnumerable<string> keys);
        void SetAll<T>(IDictionary<string, T> values);
        void SetExpiration(string cacheKey, TimeSpan expiresIn);
        void SetExpiration(string cacheKey, DateTime expiresAt);
    }
}
