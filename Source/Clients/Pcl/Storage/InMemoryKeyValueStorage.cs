using System;
using System.Collections.Generic;

namespace Exceptionless.Storage {
    public class InMemoryKeyValueStorage : IKeyValueStorage {
        private readonly Dictionary<string, object> _storage = new Dictionary<string, object>();
        private readonly object _lock = new object();

        public object Get(string key) {
            lock (_lock)
                return _storage[key];
        }

        public void Set(string key, object value) {
            lock (_lock)
            _storage[key] = value;
        }
    }
}
