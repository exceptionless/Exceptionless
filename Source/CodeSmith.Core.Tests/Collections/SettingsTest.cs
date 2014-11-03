using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace CodeSmith.Core.Tests.Collections
{
    [TestFixture]
    public class SettingsTest
    {
        [SetUp]
        public void Setup()
        {
            //TODO: NUnit setup
        }

        [TearDown]
        public void TearDown()
        {
            //TODO: NUnit TearDown
        }

        [Test]
        public void Example()
        {
            Setting.Default.InstallDate = DateTime.Now;
            Setting.Default.Save();
        }
    }
}
