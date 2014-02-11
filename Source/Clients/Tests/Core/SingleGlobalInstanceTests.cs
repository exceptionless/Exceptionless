#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Utility;
using Xunit;
using Xunit.Helpers;

namespace Exceptionless.Client.Tests.Core {
    public class SingleGlobalInstanceTests : MarshalByRefObject {
        [Fact]
        public void CanNestLockForSameKey() {
            using (new SingleGlobalInstance("key1"))
                using (new SingleGlobalInstance("key1")) {}
        }

        [Fact]
        public void LockWillTimeout() {
            using (new SingleGlobalInstance("key1")) {
                Task.Factory.StartNew(() => {
                    Assert.Throws<TimeoutException>(() => { using (new SingleGlobalInstance("key1", 100)) {} });
                }).Wait();
            }
        }

        [PartialTrustFact]
        public void LockWillTimeoutInMediumTrust() {
            using (new SingleGlobalInstance("key1")) {
                Task.Factory.StartNew(() => {
                    Assert.Throws<TimeoutException>(() => { using (new SingleGlobalInstance("key1", 100)) { } });
                }).Wait();
            }
        }

        [Fact]
        public void SupportsFullTrust() {
            bool hasLock = false;
            Parallel.For(0, 10, i => {
                using (new SingleGlobalInstance("key1")) {
                    Assert.False(hasLock, "Somebody else has the lock already.");
                    hasLock = true;
                    Console.WriteLine("Count: {0}", i);
                    Thread.Sleep(100);
                    hasLock = false;
                }
            });
        }

        [PartialTrustFact]
        public void SupportsMediumTrust() {
            bool hasLock = false;
            Parallel.For(0, 10, i => {
                using (new SingleGlobalInstance("key1")) {
                    Assert.False(hasLock, "Somebody else has the lock already.");
                    hasLock = true;
                    Console.WriteLine("Count: {0}", i);
                    Thread.Sleep(100);
                    hasLock = false;
                }
            });
        }
    }
}