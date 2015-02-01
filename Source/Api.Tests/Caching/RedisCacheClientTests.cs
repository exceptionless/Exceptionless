using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Caching;
using Exceptionless.Models;
using StackExchange.Redis;
using Xunit;

namespace Exceptionless.Api.Tests.Caching {
    public class RedisCacheClientTests {
        private readonly RedisCacheClient _cache;

        public RedisCacheClientTests() {
            if (Settings.Current.RedisConnectionString == null)
                return;

            var muxer = ConnectionMultiplexer.Connect(Settings.Current.RedisConnectionString);
            _cache = new RedisCacheClient(muxer);
        }

        [Fact]
        public void CanSetAndGetValue() {
            if (_cache == null)
                return;

            _cache.Set("test", 1);
            var value = _cache.Get<int>("test");
            Assert.Equal(1, value);
        }

        [Fact]
        public void CanSetAndGetEpiration() {
            var expiresAt = DateTime.UtcNow.AddMilliseconds(50);
            _cache.Set("test", 1, expiresAt);
            Assert.Equal(expiresAt.ToString(), _cache.GetExpiration("test").Value.ToString());
            
            Task.Delay(TimeSpan.FromMilliseconds(100)).Wait();
            Assert.Null(_cache.GetExpiration("test"));
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
