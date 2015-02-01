using System;
using System.Threading.Tasks;
using Exceptionless.Core.Caching;
using Exceptionless.Models;
using Xunit;

namespace Exceptionless.Api.Tests.Caching {
    public abstract class CacheClientTestsBase {
        protected abstract ICacheClient GetCache();

        [Fact]
        public virtual void CanSetAndGetValue() {
            var cache = GetCache();
            if (cache == null)
                return;
            
            cache.FlushAll();

            cache.Set("test", 1);
            var value = cache.Get<int>("test");
            Assert.Equal(1, value);
        }

        [Fact]
        public virtual void CanSetAndGetObject() {
            var cache = GetCache();
            if (cache == null)
                return;

            cache.FlushAll();

            var dt = DateTimeOffset.Now;
            cache.Set("test", new Event { Type = "test", Date = dt, Message = "Hello World" });
            var value = cache.Get<Event>("test");
            Assert.NotNull(value);
            Assert.Equal(dt, value.Date);
            Assert.Equal("Hello World", value.Message);
            Assert.Equal("test", value.Type);
        }

        [Fact]
        public virtual void CanSetEpiration() {
            var cache = GetCache();
            if (cache == null)
                return;

            cache.FlushAll();

            var expiresAt = DateTime.UtcNow.AddMilliseconds(50);
            cache.Set("test", 1, expiresAt);
            Assert.Equal(1, cache.Get<int>("test"));
            Assert.Equal(expiresAt.ToString(), cache.GetExpiration("test").Value.ToString());
     
            Task.Delay(TimeSpan.FromMilliseconds(100)).Wait();
            Assert.Equal(0, cache.Get<int>("test"));
            Assert.Null(cache.GetExpiration("test"));
        }
    }
}