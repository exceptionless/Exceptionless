using System;
using System.Diagnostics;
using Exceptionless.Core.Caching;
using Xunit;

namespace Exceptionless.Api.Tests.Caching {
    public class InMemoryCacheClientTests : CacheClientTestsBase {
        protected override ICacheClient GetCache() {
            return new InMemoryCacheClient();
        }

        [Fact]
        public void CanSetMaxItems() {
            var cache = GetCache() as InMemoryCacheClient;
            if (cache == null)
                return;

            cache.FlushAll();

            cache.MaxItems = 10;
            for (int i = 0; i < cache.MaxItems; i++)
                cache.Set("test" + i, i);

            Debug.WriteLine(String.Join(",", cache.Keys));
            Assert.Equal(10, cache.Count);
            cache.Set("next", 1);
            Debug.WriteLine(String.Join(",", cache.Keys));
            Assert.Equal(10, cache.Count);
            Assert.Null(cache.Get<int?>("test0"));
            Assert.NotNull(cache.Get<int?>("test1"));
            cache.Set("next2", 2);
            Debug.WriteLine(String.Join(",", cache.Keys));
            Assert.Null(cache.Get<int?>("test2"));
            Assert.NotNull(cache.Get<int?>("test1"));
        }
    }
}