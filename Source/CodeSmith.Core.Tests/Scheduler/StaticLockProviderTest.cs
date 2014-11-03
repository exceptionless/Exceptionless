using System;
using CodeSmith.Core.Scheduler;
using NUnit.Framework;

namespace CodeSmith.Core.Tests.Scheduler {
    [TestFixture]
    public class StaticLockProviderTest {
        [Test]
        public void VerifyStaticLockProviderTest() {
            var provider = new StaticLockProvider();
            var v1 = provider.Acquire("Test");
            Console.WriteLine(v1);
            Assert.IsTrue(v1.LockAcquired);

            var v2 = provider.Acquire("Test");
            Console.WriteLine(v2);
            Assert.IsFalse(v2.LockAcquired);

            v1.Dispose();

            var v3 = provider.Acquire("Test");
            Console.WriteLine(v3);
            Assert.IsTrue(v3.LockAcquired);

        }
    }
}