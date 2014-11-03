using System;
using CodeSmith.Core.Extensions;
using NUnit.Framework;

namespace CodeSmith.Core.Tests {
    [TestFixture]
    public class DateTimeRangeTests {
        [Test]
        public void CanParseNamedRanges() {
            var now = DateTime.Now;

            var range = DateTimeRange.Parse("today", now);
            Assert.AreEqual(now.Date, range.Start);
            Assert.AreEqual(now.ToEndOfDay(), range.End);

            range = DateTimeRange.Parse("yesterday", now);
            Assert.AreEqual(now.Date.SubtractDays(1), range.Start);
            Assert.AreEqual(now.Date.SubtractDays(1).ToEndOfDay(), range.End);

            range = DateTimeRange.Parse("tomorrow", now);
            Assert.AreEqual(now.Date.AddDays(1), range.Start);
            Assert.AreEqual(now.Date.AddDays(1).ToEndOfDay(), range.End);

            range = DateTimeRange.Parse("last 5 minutes", now);
            Assert.AreEqual(now.Floor(TimeSpan.FromMinutes(1)).SubtractMinutes(5), range.Start);
            Assert.AreEqual(now.Floor(TimeSpan.FromMinutes(1)), range.End);

            range = DateTimeRange.Parse("this 5 minutes", now);
            Assert.AreEqual(now.Floor(TimeSpan.FromMinutes(1)).SubtractMinutes(4), range.Start);
            Assert.AreEqual(now, range.End);

            range = DateTimeRange.Parse("next 5 minutes", now);
            Assert.AreEqual(now.Floor(TimeSpan.FromMinutes(1)), range.Start);
            Assert.AreEqual(now.Floor(TimeSpan.FromMinutes(1)).AddMinutes(5), range.End);
        }
    }
}
