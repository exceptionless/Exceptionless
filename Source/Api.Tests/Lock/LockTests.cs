using System;
using System.Threading.Tasks;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Lock;
using Xunit;

namespace Exceptionless.Api.Tests {
    public class LockTests {
        protected ICacheClient _cacheClient;
        protected ILockProvider _locker;

        public LockTests() {
            _cacheClient = new InMemoryCacheClient();
            _locker = new CacheLockProvider(_cacheClient);
        }

        [Fact]
        public void CanAcquireAndReleaseLock() {
            _cacheClient.Remove("counter");
            _locker.ReleaseLock("test");

            using (_locker.AcquireLock("test", acquireTimeout: TimeSpan.FromSeconds(1))) {
                Assert.NotNull(_cacheClient.Get<DateTime?>("lock:test"));
                Assert.Throws<TimeoutException>(() => _locker.AcquireLock("test", acquireTimeout: TimeSpan.FromMilliseconds(50)));
            }

            Assert.Null(_cacheClient.Get<DateTime?>("lock:test"));

            Parallel.For(0, 20, i => {
                using (_locker.AcquireLock("test")) {
                    Assert.NotNull(_cacheClient.Get<DateTime?>("lock:test"));
                    _cacheClient.Increment("counter", 1);
                }
            });

            var count = _cacheClient.Get<long>("counter");
            Assert.Equal(20, count);
        }

        [Fact]
        public void LockWillTimeout() {
            _locker.ReleaseLock("test");

            var testLock = _locker.AcquireLock("test", TimeSpan.FromSeconds(1));
            Assert.NotNull(testLock);

            Assert.Throws<TimeoutException>(() => _locker.AcquireLock("test", acquireTimeout: TimeSpan.FromMilliseconds(100)));

            testLock = _locker.AcquireLock("test", acquireTimeout: TimeSpan.FromSeconds(2));
            Assert.NotNull(testLock);
        }
    }
}
