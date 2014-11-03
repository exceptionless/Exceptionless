using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace CodeSmith.Core.Tests
{
    [TestFixture]
    public class BusinessDayTests
    {
        [Test]
        public void BusinessHours()
        {
            var day = new BusinessDay(DateTime.Today.DayOfWeek,
                DateTime.Now.Subtract(TimeSpan.FromHours(1)).TimeOfDay,
                DateTime.Now.AddHours(1).TimeOfDay);

            bool isDay = day.IsBusinessDay(DateTime.Now);
            Assert.IsTrue(isDay);

            // day of week test
            isDay = day.IsBusinessDay(DateTime.Now.AddDays(1));
            Assert.IsFalse(isDay);

            // to early test
            isDay = day.IsBusinessDay(DateTime.Today);
            Assert.IsFalse(isDay);

            // to late test
            isDay = day.IsBusinessDay(DateTime.Now.AddHours(2));
            Assert.IsFalse(isDay);
        }

        [Test]
        public void TotalBusinessHours()
        {
            var startDate = new DateTime(2010, 1, 1);
            var endDate = new DateTime(2010, 1, 2);
            var businessWeek = BusinessWeek.DefaultWeek;

            TimeSpan time = businessWeek.GetBusinessTime(startDate, endDate);
            // workday friday
            Assert.AreEqual(8, time.TotalHours);

            startDate = new DateTime(2010, 1, 1);
            endDate = new DateTime(2010, 1, 3);

            time = businessWeek.GetBusinessTime(startDate, endDate);
            // workday friday
            Assert.AreEqual(8, time.TotalHours);

            startDate = new DateTime(2010, 1, 1, 12, 0, 0);
            endDate = new DateTime(2010, 1, 1, 16, 0, 0);

            time = businessWeek.GetBusinessTime(startDate, endDate);
            Assert.AreEqual(4, time.TotalHours);

            startDate = new DateTime(2010, 1, 1, 6, 0, 0);
            endDate = new DateTime(2010, 1, 1, 12, 0, 0);

            time = businessWeek.GetBusinessTime(startDate, endDate);
            Assert.AreEqual(3, time.TotalHours);

            startDate = new DateTime(2010, 1, 3, 0, 0, 0);
            endDate = new DateTime(2010, 1, 10, 0, 0, 0);

            time = businessWeek.GetBusinessTime(startDate, endDate);
            Assert.AreEqual(40, time.TotalHours);
        }

        [Test]
        public void NextBusinessDay()
        {
            var businessWeek = BusinessWeek.DefaultWeek;

            BusinessDay businessDay;
            DateTime resultDate;

            var result = businessWeek.NextBusinessDay(new DateTime(2010, 1, 1, 2, 0, 0), out resultDate, out businessDay);
            Assert.IsTrue(result);
            Assert.AreEqual(new DateTime(2010, 1, 1, 9, 0, 0), resultDate);
            Assert.IsNotNull(businessDay);
            Assert.AreEqual(DayOfWeek.Friday, businessDay.DayOfWeek);

            result = businessWeek.NextBusinessDay(new DateTime(2010, 1, 1, 11, 0, 0), out resultDate, out businessDay);
            Assert.IsTrue(result);
            Assert.AreEqual(new DateTime(2010, 1, 1, 11, 0, 0), resultDate);
            Assert.IsNotNull(businessDay);
            Assert.AreEqual(DayOfWeek.Friday, businessDay.DayOfWeek);

            result = businessWeek.NextBusinessDay(new DateTime(2010, 1, 2, 11, 0, 0), out resultDate, out businessDay);
            Assert.IsTrue(result);
            Assert.AreEqual(new DateTime(2010, 1, 4, 9, 0, 0), resultDate);
            Assert.IsNotNull(businessDay);
            Assert.AreEqual(DayOfWeek.Monday, businessDay.DayOfWeek);

        }

        [Test]
        public void GetBusinessTime()
        {
            var startDate = new DateTime(2010, 1, 4, 9, 31, 30);
            var endDate = new DateTime(2010, 1, 6, 13, 14, 16);
            var businessWeek = BusinessWeek.DefaultWeek;

            Stopwatch watch = Stopwatch.StartNew();
            TimeSpan time = businessWeek.GetBusinessTime(startDate, endDate);
            watch.Stop();

            Console.WriteLine("Business Time: {0}", time);
            Console.WriteLine("Time: {0} ms", watch.ElapsedMilliseconds);
            Assert.AreEqual(new TimeSpan(19, 42, 46), time);

            startDate = new DateTime(2010, 1, 4, 9, 31, 30);
            endDate = new DateTime(2010, 1, 30, 13, 14, 16);

            watch.Reset();
            watch.Start();
            time = businessWeek.GetBusinessTime(startDate, endDate);
            watch.Stop();

            Console.WriteLine("Business Time: {0}", time);
            Console.WriteLine("Time: {0} ms", watch.ElapsedMilliseconds);
            Assert.AreEqual(new TimeSpan(6, 15, 28, 30), time);

            startDate = new DateTime(2010, 1, 4, 9, 31, 30);
            endDate = new DateTime(2010, 6, 30, 13, 14, 16);

            watch.Reset();
            watch.Start();
            time = businessWeek.GetBusinessTime(startDate, endDate);
            watch.Stop();

            Console.WriteLine("Business Time: {0}", time);
            Console.WriteLine("Time: {0} ms", watch.ElapsedMilliseconds);
            Assert.AreEqual(new TimeSpan(42, 11, 42, 46), time);
        }

        [Test]
        public void GetBusinessDate()
        {
            var startDate = new DateTime(2010, 1, 1);
            var endDate = new DateTime(2010, 1, 4, 11, 0, 0);
            var businessWeek = BusinessWeek.DefaultWeek;

            Stopwatch watch = Stopwatch.StartNew();
            var resultDate = businessWeek.GetBusinessEndDate(startDate, TimeSpan.FromHours(10));
            watch.Stop();
            Console.WriteLine("Business Date: {0}", resultDate);
            Console.WriteLine("Time: {0} ms", watch.ElapsedMilliseconds);

            Assert.AreEqual(endDate, resultDate);

            startDate = new DateTime(2010, 1, 4, 9, 31, 30);

            watch.Reset();
            watch.Start();
            resultDate = businessWeek.GetBusinessEndDate(startDate, new TimeSpan(60, 10, 15, 28));
            watch.Stop();

            Console.WriteLine("Business Date: {0}", resultDate);
            Console.WriteLine("Time: {0} ms", watch.ElapsedMilliseconds);
        }

        [Test]
        public void ThridShift()
        {
            BusinessWeek businessWeek = new BusinessWeek();
            //day 1
            businessWeek.BusinessDays.Add(new BusinessDay(DayOfWeek.Sunday, TimeSpan.FromHours(22), TimeSpan.FromHours(24)));
            businessWeek.BusinessDays.Add(new BusinessDay(DayOfWeek.Monday, TimeSpan.Zero, TimeSpan.FromHours(6)));
            //day 2
            businessWeek.BusinessDays.Add(new BusinessDay(DayOfWeek.Monday, TimeSpan.FromHours(22), TimeSpan.FromHours(24)));
            businessWeek.BusinessDays.Add(new BusinessDay(DayOfWeek.Tuesday, TimeSpan.Zero, TimeSpan.FromHours(6)));
            //day 3
            businessWeek.BusinessDays.Add(new BusinessDay(DayOfWeek.Tuesday, TimeSpan.FromHours(22), TimeSpan.FromHours(24)));
            businessWeek.BusinessDays.Add(new BusinessDay(DayOfWeek.Wednesday, TimeSpan.Zero, TimeSpan.FromHours(6)));
            //day 4
            businessWeek.BusinessDays.Add(new BusinessDay(DayOfWeek.Wednesday, TimeSpan.FromHours(22), TimeSpan.FromHours(24)));
            businessWeek.BusinessDays.Add(new BusinessDay(DayOfWeek.Thursday, TimeSpan.Zero, TimeSpan.FromHours(6)));
            //day 5
            businessWeek.BusinessDays.Add(new BusinessDay(DayOfWeek.Thursday, TimeSpan.FromHours(22), TimeSpan.FromHours(24)));
            businessWeek.BusinessDays.Add(new BusinessDay(DayOfWeek.Friday, TimeSpan.Zero, TimeSpan.FromHours(6)));


            var startDate = new DateTime(2010, 1, 3, 22, 0, 0);
            var endDate = new DateTime(2010, 1, 4, 6, 0, 0);

            TimeSpan time = businessWeek.GetBusinessTime(startDate, endDate);
            
            Assert.AreEqual(8, time.TotalHours);

            startDate = new DateTime(2010, 1, 2, 0, 0, 0);
            endDate = new DateTime(2010, 1, 9, 0, 0, 0);

            time = businessWeek.GetBusinessTime(startDate, endDate);

            Assert.AreEqual(40, time.TotalHours);

        }
    }
}
