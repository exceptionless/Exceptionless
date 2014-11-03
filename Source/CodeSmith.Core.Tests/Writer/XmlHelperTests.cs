using System;
using NUnit.Framework;
using CodeSmith.Core.Helpers;

namespace CodeSmith.Core.Tests {
    [TestFixture]
    public class XmlHelperTests {
        [Test, Ignore]
        public void GetNamespaces() {
            var namespaces = XmlHelper.GetNamespaces("<?xml version=\"1.0\"?><configuration xmlns=\"http://schemas.microsoft.com/.NetConfiguration/v2.0\"><test /></configuration>");

            Assert.IsNotNull(namespaces);
            Assert.AreEqual(1, namespaces.Count);
        }
    }
}