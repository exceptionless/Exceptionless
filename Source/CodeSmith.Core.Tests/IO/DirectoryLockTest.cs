using System;
using System.Threading;
using CodeSmith.Core.IO;
using NUnit.Framework;

namespace CodeSmith.Core.Tests.IO {
    [TestFixture, Ignore("This test requires a large amount of time to execute.")]
    public class DirectoryLockTest {
        private readonly string _directory = System.IO.Path.GetTempPath();

        [SetUp]
        public void SetUp() {
            DirectoryLock.ForceReleaseLock(_directory, true);
        }

        [Test]
        [ExpectedException(typeof(TimeoutException))]
        public void AquireTimeOut() {
            Console.WriteLine("Acquire Lock 1");
            var lock1 = DirectoryLock.Acquire(_directory);

            Console.WriteLine("Acquire Lock 2");
            var lock2 = DirectoryLock.Acquire(_directory, TimeSpan.FromSeconds(1));
        }

        [Test]
        public void Aquire() {
            var thread1 = new Thread(s => {
                Console.WriteLine("[Thread: {0}] Lock 1 Entry", Thread.CurrentThread.ManagedThreadId);
                using (var lock1 = DirectoryLock.Acquire(_directory, TimeSpan.FromSeconds(5))) {
                    Console.WriteLine("[Thread: {0}] Lock 1", Thread.CurrentThread.ManagedThreadId);
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                }
            });
            thread1.Start();

            var thread2 = new Thread(s => {
                Console.WriteLine("[Thread: {0}] Lock 2 Entry", Thread.CurrentThread.ManagedThreadId);
                using (var lock2 = DirectoryLock.Acquire(_directory, TimeSpan.FromSeconds(5))) {
                    Console.WriteLine("[Thread: {0}] Lock 2", Thread.CurrentThread.ManagedThreadId);
                }
            });
            thread2.Start();

            Thread.Sleep(TimeSpan.FromSeconds(20));
        }
    }
}