using System;
using Exceptionless.Core;
using Exceptionless.Core.Caching;
using StackExchange.Redis;

namespace Exceptionless.Api.Tests.Caching {
    public class RedisCacheClientTests: CacheClientTestsBase {
        protected override ICacheClient GetCache() {
            if (String.IsNullOrEmpty(Settings.Current.RedisConnectionString))
                return null;

            var muxer = ConnectionMultiplexer.Connect(Settings.Current.RedisConnectionString);
            return new RedisCacheClient(muxer);
        }
    }
}
