using System;

namespace Exceptionless.Core.Lock {
    public interface ILockProvider {
        IDisposable AcquireLock(string name, TimeSpan? lockTimeout = null, TimeSpan? acquireTimeout = null);
        bool IsLocked(string name);
        void ReleaseLock(string name);
    }
}
