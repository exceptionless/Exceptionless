using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeSmith.Core.Collections;
using NUnit.Framework;

namespace CodeSmith.Core.Tests.Collections
{
    [TestFixture]
    public class DelimitedStringCollectionTests
    {
        [Test]
        public void Parse()
        {
            string text = "Ten Thousand,10000, 2710 ,,\"10,000\",\"It's \"\"10 Grand\"\", baby\",10K";
            foreach (string s in DelimitedStringCollection.Parse(text, ','))
            {
                Console.WriteLine(s);
            }
        }
    }
}
