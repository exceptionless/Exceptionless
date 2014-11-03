using System;
using System.Collections.Generic;
using System.Text;
using CodeSmith.Core.Helpers;
using CodeSmith.Core.Helpers.SelfSerializerStrategy;
using NUnit.Framework;


namespace CodeSmith.Core.Tests
{
    [TestFixture]
    public class SelfSerializerTest
    {
        [Test]
        public void CanCreateSelfSerializer()
        {
            SelfSerializer<TestItem> ss = new SelfSerializer<TestItem>();
            Assert.IsNotNull(ss);
        }
    }
}
