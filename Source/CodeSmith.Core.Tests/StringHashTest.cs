using System;
using CodeSmith.Core.Extensions;
using NUnit.Framework;

namespace CodeSmith.Core.Tests {
    [TestFixture]
    public class StringHashTest {
        [Test]
        public void GetMd5Hash() {
            string hash = HashExtensions.ToMD5("this is a test to hash");

            Assert.AreEqual(32, hash.Length);
        }

        [Test]
        public void SHA1Hash() {
            string hash = "this is a test hash".ToSHA1();
            Assert.AreEqual(40, hash.Length);


            hash = "this is a test hash of some longer tests".ToSHA1();
            Assert.AreEqual(40, hash.Length);


            hash = "short".ToSHA1();
            Assert.AreEqual(40, hash.Length);
        }
    }
}