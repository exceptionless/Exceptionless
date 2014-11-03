using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using CodeSmith.Core.Extensions;

namespace CodeSmith.Core.Tests.Extensions
{
    [TestFixture]
    public class TimeSpanTests
    {
        [Test]
        public void ToWords()
        {
            TimeSpan value = TimeSpan.FromMilliseconds(100);
            Assert.AreEqual("0.1 second", value.ToWords());
            
            value = TimeSpan.FromMilliseconds(-100);
            Assert.AreEqual("-0.1 second", value.ToWords());

            value = TimeSpan.FromMilliseconds(100);
            Assert.AreEqual("0.1s", value.ToWords(true));

            value = TimeSpan.FromMilliseconds(2500);
            Assert.AreEqual("2.5 seconds", value.ToWords());

            value = TimeSpan.FromMilliseconds(2500);
            Assert.AreEqual("2.5s", value.ToWords(true));
            
            value = TimeSpan.FromMilliseconds(16500);
            Assert.AreEqual("16 seconds", value.ToWords());

            value = TimeSpan.FromMilliseconds(16500);
            Assert.AreEqual("16s", value.ToWords(true));

            value = TimeSpan.FromHours(6);
            Assert.AreEqual("6 hours", value.ToWords());

            value = TimeSpan.FromHours(-6);
            Assert.AreEqual("-6 hours", value.ToWords());

            value = TimeSpan.FromHours(6);
            Assert.AreEqual("6h", value.ToWords(true));

            value = TimeSpan.FromMinutes(186);
            Assert.AreEqual("3 hours 6 minutes", value.ToWords());

            value = TimeSpan.FromMinutes(-186);
            Assert.AreEqual("-3 hours 6 minutes", value.ToWords());

            value = TimeSpan.FromMinutes(186);
            Assert.AreEqual("3h 6m", value.ToWords(true));

            value = TimeSpan.FromDays(10.15);
            Assert.AreEqual("1 week 3 days", value.ToWords(false, 2));

            value = TimeSpan.FromDays(10.15);
            Assert.AreEqual("1w 3d 3h 36m", value.ToWords(true));
        }

        [Test]
        public void ApproximateAge() {
            Assert.AreEqual("Just now", DateTime.Now.AddMilliseconds(-100).ToApproximateAgeString());
            Assert.AreEqual("Just now", DateTime.Now.AddSeconds(-50).ToApproximateAgeString());
            Assert.AreEqual("1 minute ago", DateTime.Now.AddSeconds(-65).ToApproximateAgeString());
            Assert.AreEqual("1 hour ago", DateTime.Now.AddMinutes(-61).ToApproximateAgeString());
            Assert.AreEqual("1 day ago", DateTime.Now.AddHours(-24).ToApproximateAgeString());
            Assert.AreEqual("2 days ago", DateTime.Now.AddHours(-48).ToApproximateAgeString());
            Assert.AreEqual("1 week ago", DateTime.Now.AddDays(-7).ToApproximateAgeString());
        }
    }
}
