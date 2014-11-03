using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using CodeSmith.Core.Extensions;

namespace CodeSmith.Core.Tests.Extensions {
    [TestFixture]
    public class StringExtensionsTest {
        [Test]
        public void ReplaceMultipleRegexTest() {
            var s = "1hello2world3hello4world5helloworld6worldhello7helloworldhelloworld8";
            var d = new Dictionary<string, string>();
            d.Add("hell[o]", "A");
            d.Add("[w]o[r]l[d]", "B");

            var r1 = s.ReplaceMultiple(d, true);
            Assert.AreEqual(r1, "1A2B3A4B5AB6BA7ABAB8");
        }

        [Test]
        public void ReplaceMultipleTest() {
            var s = "1hello2world3hello4world5helloworld6worldhello7helloworldhelloworld8";
            var d = new Dictionary<string, string>();
            d.Add("hello", "A");
            d.Add("world", "B");

            var r1 = s.ReplaceMultiple(d);
            Assert.AreEqual(r1, "1A2B3A4B5AB6BA7ABAB8");
        }

        [Test]
        public void ToIdentifierTests() {
            Assert.AreEqual("_01_NumberInClassName", "01_Number-In.Class.Name".ToIdentifier());
            Assert.AreEqual("___Test", "__Test".ToIdentifier());
        }
    }
}