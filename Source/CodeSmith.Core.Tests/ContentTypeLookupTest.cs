using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace CodeSmith.Core.Tests
{
    [TestFixture]
    public class ContentTypeLookupTest
    {

        [Test]
        public void GetByExtension()
        {
            string contentType = ContentType.GetByExtension(".xml");
            Assert.AreEqual("text/xml", contentType);

            contentType = ContentType.GetByExtension(".db");

            contentType = ContentType.GetByExtension(".blah");
        }
    }
}
