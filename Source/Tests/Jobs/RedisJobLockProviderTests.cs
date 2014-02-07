#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using CodeSmith.Core.Scheduler;
using Exceptionless.Core.Jobs;
using Exceptionless.Tests.Utility;
using ServiceStack.CacheAccess;
using Xunit;

namespace Exceptionless.Tests.Jobs {
    public class RedisJobLockProviderTests {
        private readonly ICacheClient _cacheClient;

        public RedisJobLockProviderTests() {
            _cacheClient = IoC.GetInstance<ICacheClient>();
        }

        [Fact]
        public void CanLock() {
            var provider = new RedisJobLockProvider(_cacheClient);
            JobLock v1 = provider.Acquire("Test");
            Console.WriteLine(v1);
            Assert.True(v1.LockAcquired);

            JobLock v2 = provider.Acquire("Test");
            Console.WriteLine(v2);
            Assert.False(v2.LockAcquired);

            v1.Dispose();

            JobLock v3 = provider.Acquire("Test");
            Console.WriteLine(v3);
            Assert.True(v3.LockAcquired);

            v2.Dispose();
            v3.Dispose();
        }

        [Fact]
        public void CanMachineLock() {
            var provider = new RedisJobLockProvider(_cacheClient);
            JobLock v1 = provider.Acquire("Test");
            Console.WriteLine(v1);
            Assert.True(v1.LockAcquired);

            JobLock v2 = provider.Acquire("Test");
            Console.WriteLine(v2);
            Assert.False(v2.LockAcquired);

            v1.Dispose();

            JobLock v3 = provider.Acquire("Test");
            Console.WriteLine(v3);
            Assert.True(v3.LockAcquired);

            v2.Dispose();
            v3.Dispose();
        }
    }
}