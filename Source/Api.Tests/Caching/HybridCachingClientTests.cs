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
            var cache = GetCache() as HybridCacheClient;
            Assert.NotNull(cache);
             
            var secondCache = GetCache() as HybridCacheClient;
            Assert.NotNull(secondCache);
            
            cache.FlushAll();

            cache.Set("test", 1);
            secondCache.Set("test2", 1, TimeSpan.FromMilliseconds(50));
            var value = cache.Get<int>("test");
            Assert.Equal(1, value);
            Assert.Equal(1, cache.LocalCache.Count);
            Assert.Equal(1, secondCache.LocalCache.Count);

            secondCache.Remove("test");
            Task.Delay(TimeSpan.FromMilliseconds(600)).Wait();
            Assert.Equal(0, cache.LocalCache.Count);
            Assert.Equal(0, secondCache.LocalCache.Count);
        }
    }
}
