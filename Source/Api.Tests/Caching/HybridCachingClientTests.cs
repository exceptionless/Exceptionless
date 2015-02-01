using System;
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
    }
}
