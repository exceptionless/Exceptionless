using System;
using Exceptionless.Storage;

namespace Exceptionless.Extras.Storage {
    public class IsolatedStorageKeyValueStorage : IKeyValueStorage {
        private readonly IsolatedStorageFileStorage _storage = new IsolatedStorageFileStorage();

        public object Get(string group, string key) {
            throw new NotImplementedException();
        }

        public void Set(string group, string key, object value) {
            // TODO: Implement some sort of timer where we save a json file to disk containing the key value entries.
        }

        public void Dispose() {
            if (_storage != null)
                _storage.Dispose();
        }
    }
}
