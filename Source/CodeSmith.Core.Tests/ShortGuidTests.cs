using System;
using System.Diagnostics;
using NUnit.Framework;

namespace CodeSmith.Core.Tests {
    [TestFixture]
    public class ShortGuidTests {
        [Test]
        public void ConvertGuidToShortGuidTest() {
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < 1000000; i++) {
                Guid g = Guid.NewGuid();
                ShortGuid s = g;
                string v = s.Value;

                var s2 = new ShortGuid(v);
                Assert.AreEqual(s, s2);

                Guid g2 = s2;
                Assert.AreEqual(g, g2);
            }
            watch.Stop();
            Console.WriteLine("Time: {0} ms", watch.ElapsedMilliseconds);
        }
    }
}