using System;
using System.Text;
#if !EMBEDDED
using CodeSmith.Core.Helpers;

namespace CodeSmith.Core.Extensions {
    public
#else
namespace Exceptionless.Extensions {
    internal
#endif
    static class DateTimeExtensions {
#if !EMBEDDED
        public static string ToApproximateAgeString(this DateTime fromDate) {
            var age = GetAge(fromDate);
            if (Math.Abs(age.TotalMinutes) <= 1d)
                return age.TotalSeconds > 0 ? "Just now" : "Right now";

            if (age.TotalSeconds > 0)
                return age.ToString(1) + " ago";

            return age.ToString(1) + " from now";
        }

        public static string ToAgeString(this DateTime fromDate) {
            return ToAgeString(fromDate, DateTime.Now, 0);
        }

        public static string ToAgeString(this DateTime fromDate, int maxSpans) {
            return ToAgeString(fromDate, DateTime.Now, maxSpans);
        }

        public static string ToAgeString(this DateTime fromDate, int maxSpans, bool shortForm) {
            return ToAgeString(fromDate, DateTime.Now, maxSpans, shortForm);
        }

        public static string ToAgeString(this DateTime fromDate, DateTime toDate, int maxSpans) {
            var age = GetAge(fromDate, toDate);
            return age.ToString(maxSpans, false);
        }

        public static string ToAgeString(this DateTime fromDate, DateTime toDate, int maxSpans, bool shortForm) {
            var age = GetAge(fromDate, toDate);
            return age.ToString(maxSpans, shortForm);
        }

        public static AgeSpan GetAge(this DateTime fromDate) {
            return GetAge(fromDate, DateTime.Now);
        }

        public static AgeSpan GetAge(this DateTime fromDate, DateTime toDate) {
            return new AgeSpan(toDate - fromDate);
        }
#endif

        public static int ToEpoch(this DateTime fromDate) {
            var utc = (fromDate.ToUniversalTime().Ticks - EPOCH_TICKS) / TimeSpan.TicksPerSecond;
            return Convert.ToInt32(utc);
        }

        public static int ToEpochOffset(this DateTime date, int timestamp) {
            return timestamp - date.ToEpoch();
        }

        public static int ToEpoch(this DateTime date, int offset) {
            return offset + date.ToEpoch();
        }

        private const long EPOCH_TICKS = 621355968000000000;

        public static DateTime ToDateTime(this int secondsSinceEpoch) {
            return new DateTime(EPOCH_TICKS + (secondsSinceEpoch * TimeSpan.TicksPerSecond)); 
        }

        public static DateTime ToDateTime(this double milliSecondsSinceEpoch) {
            return new DateTime(EPOCH_TICKS + ((long)milliSecondsSinceEpoch * TimeSpan.TicksPerMillisecond));
        }

        /// <summary>
        /// Adjust the DateTime so the time is 1 millisecond before the next day.
        /// </summary>
        /// <param name="dateTime">The DateTime to adjust.</param>
        /// <returns>A DateTime that is 1 millisecond before the next day.</returns>
        public static DateTime ToEndOfDay(this DateTime dateTime)
        {
            return dateTime.Date                            // convert to just a date with out time
                .AddDays(1)                                 // add one day so its tomorrow
                .Subtract(TimeSpan.FromMilliseconds(1));    // subtract 1 ms
        }

        public static DateTime ChangeMonth(this DateTime dateTime, int month) {
            return dateTime.AddMonths(month - dateTime.Date.Month);
        }

        public static DateTime ToBeginningOfYear(this DateTime dateTime) {
            return dateTime.Date.AddDays(1 - dateTime.Date.Day).AddMonths(1 - dateTime.Date.Month);
        }

        public static DateTime ToEndOfYear(this DateTime dateTime) {
            return dateTime.ToBeginningOfYear().AddYears(1).AddSeconds(-1);
        }

        public static DateTime ToBeginningOfMonth(this DateTime dateTime) {
            return dateTime.Date.AddDays(1 - dateTime.Date.Day);
        }

        public static DateTime ToEndOfMonth(this DateTime dateTime) {
            return dateTime.ToBeginningOfMonth().AddMonths(1).AddSeconds(-1);
        }

#if !EMBEDDED
        public static DateTime Round(this DateTime datetime, TimeSpan roundingInterval, MidpointRounding roundingType = MidpointRounding.ToEven)
        {
            return new DateTime((datetime - DateTime.MinValue).Round(roundingInterval, roundingType).Ticks, datetime.Kind);
        }

        public static DateTime Floor(this DateTime datetime, TimeSpan roundingInterval)
        {
            return new DateTime((datetime - DateTime.MinValue).Floor(roundingInterval).Ticks, datetime.Kind);
        }

        public static DateTime Ceiling(this DateTime datetime, TimeSpan roundingInterval)
        {
            return new DateTime((datetime - DateTime.MinValue).Ceiling(roundingInterval).Ticks, datetime.Kind);
        }

        public static DateTimeOffset Round(this DateTimeOffset datetime, TimeSpan roundingInterval, MidpointRounding roundingType = MidpointRounding.ToEven) {
            return new DateTimeOffset((datetime.UtcDateTime - DateTimeOffset.MinValue).Round(roundingInterval, roundingType).Ticks, datetime.Offset);
        }

        public static DateTimeOffset Floor(this DateTimeOffset datetime, TimeSpan roundingInterval) {
            return new DateTimeOffset((datetime.UtcDateTime - DateTimeOffset.MinValue).Floor(roundingInterval).Ticks, datetime.Offset);
        }

        public static DateTimeOffset Ceiling(this DateTimeOffset datetime, TimeSpan roundingInterval) {
            return new DateTimeOffset((datetime.UtcDateTime - DateTimeOffset.MinValue).Ceiling(roundingInterval).Ticks, datetime.Offset);
        }

        public static DateTimeOffset Random(this DateTimeOffset value, TimeSpan timeSpan) {
            double seconds = timeSpan.TotalSeconds * RandomHelper.Instance.NextDouble();

            // Alternatively: return value.AddSeconds(-seconds);
            TimeSpan span = TimeSpan.FromSeconds(seconds);
            return value - span;
        }
#endif

        private const UInt64 LocalMask = 0x8000000000000000;
        private const Int64 TicksCeiling = 0x4000000000000000;
        private const Int32 KindShift = 62;

        /// <summary>
        /// Serializes the current DateTime object to a 64-bit binary value that subsequently can be used to recreate the DateTime object.
        /// </summary>
        /// <param name="self">The DateTime to serialize.</param>
        /// <returns>A 64-bit signed integer that encodes the Kind and Ticks properties.</returns>
        /// <remarks>
        /// This method exists to add missing functionality in Silverlight.
        /// </remarks>
        public static long ToBinary(this DateTime self) {
            // based on .net source code
            if (self.Kind != DateTimeKind.Local)
                return (self.Ticks | ((Int64)self.Kind << KindShift));

            // Local times need to be adjusted as you move from one time zone to another, 
            // just as they are when serializing in text. As such the format for local times
            // changes to store the ticks of the UTC time, but with flags that look like a 
            // local date. 

            // To match serialization in text we need to be able to handle cases where 
            // the UTC value would be out of range. Unused parts of the ticks range are
            // used for this, so that values just past max value are stored just past the
            // end of the maximum range, and values just below minimum value are stored
            // at the end of the ticks area, just below 2^62. 
            TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(self);
            Int64 ticks = self.Ticks;
            Int64 storedTicks = ticks - offset.Ticks;
            if (storedTicks < 0)
                storedTicks = TicksCeiling + storedTicks;

            return storedTicks | (unchecked((Int64)LocalMask));
        }

        #region Subtract Extensions

        public static DateTime SubtractDays(this DateTime date, double value) {
            if (value < 0)
                throw new ArgumentException("Value cannot be less than 0.", "value");

            return date.AddDays(value * -1);
        }

        public static DateTime SubtractHours(this DateTime date, double value) {
            if (value < 0)
                throw new ArgumentException("Value cannot be less than 0.", "value");

            return date.AddHours(value * -1);
        }

        public static DateTime SubtractMilliseconds(this DateTime date, double value) {
            if (value < 0)
                throw new ArgumentException("Value cannot be less than 0.", "value");

            return date.AddMilliseconds(value * -1);
        }

        public static DateTime SubtractMinutes(this DateTime date, double value) {
            if (value < 0)
                throw new ArgumentException("Value cannot be less than 0.", "value");

            return date.AddMinutes(value * -1);
        }

        public static DateTime SubtractMonths(this DateTime date, int months) {
            if (months < 0)
                throw new ArgumentException("Months cannot be less than 0.", "months");

            return date.AddMonths(months * -1);
        }

        public static DateTime SubtractSeconds(this DateTime date, double value) {
            if (value < 0)
                throw new ArgumentException("Value cannot be less than 0.", "value");

            return date.AddSeconds(value * -1);
        }

        public static DateTime SubtractTicks(this DateTime date, long value) {
            if (value < 0)
                throw new ArgumentException("Value cannot be less than 0.", "value");

            return date.AddTicks(value * -1);
        }

        public static DateTime SubtractYears(this DateTime date, int value) {
            if (value < 0)
                throw new ArgumentException("Value cannot be less than 0.", "value");

            return date.AddYears(value * -1);
        }

        #endregion
    }
}
