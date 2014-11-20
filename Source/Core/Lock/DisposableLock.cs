using System;

namespace Exceptionless.Core.Lock {
    internal class DisposableLock : IDisposable {
        private readonly ILockProvider _lockProvider;
        private readonly string _name;

        public DisposableLock(string name, ILockProvider lockProvider) {
            _name = name;
            _lockProvider = lockProvider;
        }

        public void Dispose() {
            _lockProvider.ReleaseLock(_name);
        }
    }
}