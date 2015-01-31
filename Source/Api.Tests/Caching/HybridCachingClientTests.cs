using System;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Caching;
using Exceptionless.Models;
using StackExchange.Redis;
using Xunit;

namespace Exceptionless.Api.Tests.Caching {
    public class HybridCachingClientTests {
        private readonly HybridCacheClient _cache;

        public HybridCachingClientTests() {
            if (Settings.Current.RedisConnectionString == null)
                return;

            var muxer = ConnectionMultiplexer.Connect(Settings.Current.RedisConnectionString);
            _cache = new HybridCacheClient(muxer);
        }

        [Fact]
        public void CanSetAndGetValue() {
            if (_cache == null)
                return;

            _cache.Set("test", 1);
            var value = _cache.Get<int>("test");
            Assert.Equal(1, value);

            Assert.Equal(1, _cache.LocalCache.Count);
        }

        [Fact]
        public void CanSetExpiration() {
            if (_cache == null)
                return;

            _cache.Set("test", 1, TimeSpan.FromMilliseconds(250));
            var value = _cache.Get<int>("test");
            Assert.Equal(1, value);
            Task.Delay(TimeSpan.FromMilliseconds(300)).Wait();
            var newvalue = _cache.Get<int>("test");
            Assert.Equal(0, newvalue);
        }

        [Fact]
        public void CanSetAndGetObject() {
            if (_cache == null)
                return;

            var dt = DateTimeOffset.Now;
            _cache.Set("test", new Event { Type = "test", Date = dt, Message = "Hello World" });
            var value = _cache.Get<Event>("test");

            Assert.Equal(dt, value.Date);
            Assert.Equal("Hello World", value.Message);
            Assert.Equal("test", value.Type);
        }
    }
}
