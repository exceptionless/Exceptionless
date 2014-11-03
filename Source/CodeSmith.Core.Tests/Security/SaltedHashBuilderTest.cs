using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeSmith.Core.Security;
using NUnit.Framework;

namespace CodeSmith.Core.Tests.Security
{
    [TestFixture]
    public class SaltedHashBuilderTest
    {
        [Test]
        public void GetHash()
        {
            Guid guid = Guid.NewGuid();
            SaltedHashBuilder builder = new SaltedHashBuilder(guid.ToString());

            builder.Append(true);
            builder.Append('c');
            builder.Append(DateTime.Now);
            builder.Append("this is i a test");

            string hash = builder.GetHash();
        }
    }
}
