using System;

namespace Exceptionless.Storage {
    public interface IKeyValueStorage {
        object Get(string key);
        void Set(string key, object value);
    }
}
