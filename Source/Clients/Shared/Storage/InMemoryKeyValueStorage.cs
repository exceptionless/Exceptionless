using System;
using System.Collections.Generic;

namespace Exceptionless.Storage {
    public class InMemoryKeyValueStorage : IKeyValueStorage {
        private readonly Dictionary<string, Dictionary<string, object>> _storage = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();

        public object Get(string group, string key) {
            lock (_lock) {
                if (!_storage.ContainsKey(group))
                    _storage[group] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                return _storage[group].ContainsKey(key) ? _storage[group][key] : null;
            }
        }

        public void Set(string group, string key, object value) {
            lock (_lock) {
                if (!_storage.ContainsKey(group))
                    _storage[group] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                _storage[group][key] = value;
            }
        }

        public void Dispose() {
            if (_storage != null)
                _storage.Clear();
        }
    }
}
