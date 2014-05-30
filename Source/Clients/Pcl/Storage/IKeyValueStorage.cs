using System;

namespace Exceptionless.Storage {
    public interface IKeyValueStorage : IDisposable {
        object Get(string key);
        void Set(string key, object value);
    }
}
