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
using Exceptionless.Core;
using Exceptionless.Core.Jobs;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Tests.Jobs {
    public class MongoJobLockProviderTests : MongoRepositoryTestBase<JobLockInfo, IJobLockInfoRepository> {
        public MongoJobLockProviderTests() : base(IoC.GetInstance<IJobLockInfoRepository>(), true) {}

        [Fact]
        public void CanLock() {
            var provider = new MongoJobLockProvider(IoC.GetInstance<IJobLockInfoRepository>());
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
        public void LockWillTimeout() {
            var provider = new MongoJobLockProvider(IoC.GetInstance<IJobLockInfoRepository>());

            JobLock v1 = provider.Acquire("Test");
            Console.WriteLine(v1);
            Assert.True(v1.LockAcquired);

            JobLock v2 = provider.Acquire("Test");
            Console.WriteLine(v2);
            Assert.False(v2.LockAcquired);

            var l = Repository.FirstOrDefault(li => li.Name == "Test");
            l.CreatedDate = l.CreatedDate.Subtract(TimeSpan.FromMinutes(25));
            Repository.Update(l);

            JobLock v3 = provider.Acquire("Test");
            Console.WriteLine(v3);
            Assert.True(v3.LockAcquired);

            v1.Dispose();
            v2.Dispose();
            v3.Dispose();
        }

        [Fact]
        public void CanMachineLock() {
            var provider = new MongoMachineJobLockProvider(IoC.GetInstance<IJobLockInfoRepository>());
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