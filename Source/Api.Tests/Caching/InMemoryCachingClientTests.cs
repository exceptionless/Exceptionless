using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Exceptionless.Core.Caching;
using Xunit;

namespace Exceptionless.Api.Tests.Caching {
    public class InMemoryCacheClientTests {
        [Fact]
        public void CanSetAndGetValue() {
            var cache = new InMemoryCacheClient();
            cache.Set("test", 1);
            var value = cache.Get<int>("test");
            Assert.Equal(1, value);
        }

        [Fact]
        public void CanSetAndGetEpiration() {
            var cache = new InMemoryCacheClient();

            var expiresAt = DateTime.UtcNow.AddMilliseconds(25);
            cache.Set("test", 1, expiresAt);
            Assert.Equal(expiresAt, cache.GetExpiration("test"));
     
            Task.Delay(TimeSpan.FromMilliseconds(25)).Wait();
            Assert.Null(cache.GetExpiration("test"));
        }

        [Fact]
        public void CanSetMaxItems() {
            var cache = new InMemoryCacheClient();
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
