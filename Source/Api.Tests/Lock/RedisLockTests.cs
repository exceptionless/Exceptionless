using System;
using Exceptionless.Core;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Lock;
using StackExchange.Redis;

namespace Exceptionless.Api.Tests {
    public class RedisLockTests : LockTests {
        public RedisLockTests() {
            var muxer = ConnectionMultiplexer.Connect(Settings.Current.RedisConnectionInfo.ToString());
            _cacheClient = new RedisCacheClient(muxer.GetDatabase());
            _locker = new CacheLockProvider(_cacheClient);
        }
    }
}
