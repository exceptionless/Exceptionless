using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using CodeSmith.Core.Extensions;

namespace CodeSmith.Core.Tests.Extensions
{
    [TestFixture]
    public class DateTimeTests
    {
        
        [Test]
        public void ToAge()
        {
            DateTime d1 = new DateTime(2009, 1, 4);

            string result = d1.ToAgeString();


            d1 = new DateTime(2008, 6, 19);

            result = d1.ToAgeString();

        }


        [Test]
        public void ToAge2()
        {
            DateTime d1 = new DateTime(2009, 1, 4);

            string result = d1.ToAgeString(1);


            d1 = new DateTime(2008, 6, 19);

            result = d1.ToAgeString(1);

        }

        [Test]
        public void ToBinary()
        {
            DateTime d1 = new DateTime(2009, 1, 4);
            var a = d1.ToBinary();
            var b = DateTimeExtensions.ToBinary(d1);
            Assert.AreEqual(a, b);

            d1 = new DateTime(2008, 6, 19);
            a = d1.ToBinary();
            b = DateTimeExtensions.ToBinary(d1);
            Assert.AreEqual(a, b);

            d1 = new DateTime(2008, 6, 19, 0, 0, 0, DateTimeKind.Utc);
            a = d1.ToBinary();
            b = DateTimeExtensions.ToBinary(d1);
            Assert.AreEqual(a, b);

            d1 = DateTime.Now;
            a = d1.ToBinary();
            b = DateTimeExtensions.ToBinary(d1);
            Assert.AreEqual(a, b);

            d1 = DateTime.UtcNow;
            a = d1.ToBinary();
            b = DateTimeExtensions.ToBinary(d1);
            Assert.AreEqual(a, b);

            d1 = DateTime.Today;
            a = d1.ToBinary();
            b = DateTimeExtensions.ToBinary(d1);
            Assert.AreEqual(a, b);
        }
    }
}
