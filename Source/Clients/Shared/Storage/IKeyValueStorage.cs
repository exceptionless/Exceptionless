using System;

namespace Exceptionless.Storage {
    public interface IKeyValueStorage : IDisposable {
        object Get(string group, string key);
        void Set(string group, string key, object value);
    }
}
