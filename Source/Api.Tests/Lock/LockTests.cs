using System;
using System.Threading.Tasks;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Lock;
using Xunit;

namespace Exceptionless.Api.Tests {
    public class LockTests {
        [Fact]
        public void CanAcquireAndReleaseLock() {
            var cacheClient = new InMemoryCacheClient();
            var locker = new CacheLockProvider(cacheClient);

            using (locker.AcquireLock("test")) {
                Assert.NotNull(cacheClient.Get("lock:test"));
                Assert.Throws<TimeoutException>(() => locker.AcquireLock("test", acquireTimeout: TimeSpan.FromMilliseconds(50)));
            }

            Assert.Null(cacheClient.Get("lock:test"));

            Parallel.For(0, 20, i => {
                using (locker.AcquireLock("test")) {
                    Assert.NotNull(cacheClient.Get("lock:test"));
                    cacheClient.Increment("counter", 1);
                }
            });

            var count = cacheClient.Get<long>("counter");
            Assert.Equal(20, count);
        }
    }
}
