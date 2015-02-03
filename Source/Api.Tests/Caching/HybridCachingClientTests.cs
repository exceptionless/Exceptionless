using System;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Caching;
using StackExchange.Redis;
using Xunit;

namespace Exceptionless.Api.Tests.Caching {
    public class HybridCachingClientTests: CacheClientTestsBase {
        protected override ICacheClient GetCache() {
            if (String.IsNullOrEmpty(Settings.Current.RedisConnectionString))
                return null;

            var muxer = ConnectionMultiplexer.Connect(Settings.Current.RedisConnectionString);
            return new HybridCacheClient(muxer);
        }

        [Fact]
        public override void CanSetAndGetValue() {
            var cache = GetCache() as HybridCacheClient;
            if (cache == null)
                return;

            cache.FlushAll();

            cache.Set("test", 1);
            var value = cache.Get<int>("test");
            Assert.Equal(1, value);
            Assert.Equal(1, cache.LocalCache.Count);
        }

        [Fact]
        public void CanInvalidateLocalCache() {
            var firstCache = GetCache() as HybridCacheClient;
            Assert.NotNull(firstCache);
             
            var secondCache = GetCache() as HybridCacheClient;
            Assert.NotNull(secondCache);

            firstCache.Set("willCacheLocallyOnFirst", 1);
            Assert.Equal(1, firstCache.LocalCache.Count);
            Assert.Equal(0, secondCache.LocalCache.Count);

            secondCache.Set("keyWillExpire", 50, TimeSpan.FromMilliseconds(100));
            secondCache.Set("keyWillNotExpire", 60 * 5, TimeSpan.FromMinutes(5));
            Assert.Equal(1, firstCache.LocalCache.Count);
            Assert.Equal(2, secondCache.LocalCache.Count);

            Assert.Equal(1, firstCache.Get<int>("willCacheLocallyOnFirst"));
            Assert.Equal(1, firstCache.LocalCache.Count);
            Assert.Equal(2, secondCache.LocalCache.Count);

            Assert.Equal(50, firstCache.Get<int>("keyWillExpire"));
            Assert.Equal(2, firstCache.LocalCache.Count);
            Assert.Equal(2, secondCache.LocalCache.Count);

            // Remove key from second machine and ensure first cache is cleared.
            secondCache.Remove("willCacheLocallyOnFirst");

            // Try and wait until the published message has been process.
            var count = 0;
            while ((firstCache.LocalCache.Count != 0 || secondCache.LocalCache.Count != 1) && count < 10) {
                Task.Delay(TimeSpan.FromMilliseconds(250)).Wait();
                count++;
            }

            Assert.Equal(0, firstCache.LocalCache.Count);
            Assert.Equal(1, secondCache.LocalCache.Count);
        }
    }
}
