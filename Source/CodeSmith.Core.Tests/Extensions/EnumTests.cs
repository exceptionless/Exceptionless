using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using CodeSmith.Core.Extensions;

namespace CodeSmith.Core.Tests.Extensions
{
    [TestFixture]
    public class EnumTests
    {
        [Test]
        public void CanFindDefault()
        {
            SomeEnum t;
            Enum.TryParse("Value2", out t);
        }
    }

    public enum SomeEnum
    {
        Value1 = 12,
        Value2 = 13,
        Value3 = 14
    }
}
