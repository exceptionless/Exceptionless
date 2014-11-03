using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using CodeSmith.Core.Extensions;

namespace CodeSmith.Core.Tests.Extensions
{
    [TestFixture]
    public class NUmberExtensionsTest
    {
        [Test]
        public void ToFileSizeDisplay()
        {
            long size = 186833119;
            string sizeDisplay = size.ToFileSizeDisplay();
            Assert.AreEqual("178.18 MB", sizeDisplay);

            size = 186833119;
            sizeDisplay = size.ToFileSizeDisplay(1);
            Assert.AreEqual("178.2 MB", sizeDisplay);

            size = 18683311954;
            sizeDisplay = size.ToFileSizeDisplay();
            Assert.AreEqual("17.40 GB", sizeDisplay);
            
            size = 1868331195423;
            sizeDisplay = size.ToFileSizeDisplay();
            Assert.AreEqual("1,740.02 GB", sizeDisplay);

            size = 1024 * 1024;
            sizeDisplay = size.ToFileSizeDisplay();
            Assert.AreEqual("1 MB", sizeDisplay);

            size = 1024 * 1024 * 1024;
            sizeDisplay = size.ToFileSizeDisplay();
            Assert.AreEqual("1 GB", sizeDisplay);
        }
    }
}
