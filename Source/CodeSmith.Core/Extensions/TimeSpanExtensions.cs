using System;
using System.Text;

namespace CodeSmith.Core.Extensions
{
    public static class TimeSpanExtensions
    {
        public const double AvgDaysInAYear = 365.2425d;
        public const double AvgDaysInAMonth = 30.436875d;

        public static int GetYears(this TimeSpan timespan) {
            return (int)(timespan.TotalDays / AvgDaysInAYear);
        }

        public static double GetTotalYears(this TimeSpan timespan) {
            return timespan.TotalDays / AvgDaysInAYear;
        }

        public static int GetMonths(this TimeSpan timespan) {
            return (int)((timespan.TotalDays % AvgDaysInAYear) / AvgDaysInAMonth);
        }

        public static double GetTotalMonths(this TimeSpan timespan) {
            return timespan.TotalDays / AvgDaysInAMonth;
        }

        public static int GetWeeks(this TimeSpan timespan) {
            return (int)(((timespan.TotalDays % AvgDaysInAYear) % AvgDaysInAMonth) / 7d);
        }

        public static double GetTotalWeeks(this TimeSpan timespan) {
            return timespan.TotalDays / 7d;
        }

        public static int GetDays(this TimeSpan timespan) {
            return (int)(timespan.TotalDays % 7d);
        }
        
        public static double GetMicroseconds(this TimeSpan span)
        {
            return span.Ticks / 10d;
        }

        public static double GetNanoseconds(this TimeSpan span)
        {
            return span.Ticks / 100d;
        }

        public static TimeSpan Round(this TimeSpan time, TimeSpan roundingInterval, MidpointRounding roundingType = MidpointRounding.ToEven)
        {
            return new TimeSpan(Convert.ToInt64(Math.Round(time.Ticks / (decimal)roundingInterval.Ticks, roundingType)) * roundingInterval.Ticks);
        }

        public static TimeSpan Floor(this TimeSpan time, TimeSpan roundingInterval)
        {
            return new TimeSpan(Convert.ToInt64(Math.Floor(time.Ticks / (decimal)roundingInterval.Ticks)) * roundingInterval.Ticks);
        }

        public static TimeSpan Ceiling(this TimeSpan time, TimeSpan roundingInterval)
        {
            return new TimeSpan(Convert.ToInt64(Math.Ceiling(time.Ticks / (decimal)roundingInterval.Ticks)) * roundingInterval.Ticks);
        }

        public static string ToWords(this TimeSpan span)
        {
            return ToWords(span, false, -1);
        }

        public static string ToWords(this TimeSpan span, bool shortForm)
        {
            return ToWords(span, shortForm, -1);
        }

        public static string ToWords(this TimeSpan span, bool shortForm, int maxParts) {
            var age = new AgeSpan(span);
            return age.ToString(maxParts, shortForm);
        }

        public static AgeSpan ToAgeSpan(this TimeSpan span) {
            return new AgeSpan(span);
        }
    }

    public struct AgeSpan {
        public AgeSpan(TimeSpan span) : this() {
            TotalYears = span.GetTotalYears();
            Years = span.GetYears();
            TotalMonths = span.GetTotalMonths();
            Months = span.GetMonths();
            TotalWeeks = span.GetTotalWeeks();
            Weeks = span.GetWeeks();
            Days = span.GetDays();
            TimeSpan = span;
        }

        public double TotalYears { get; private set; }
        public int Years { get; private set; }
        public double TotalMonths { get; private set; }
        public int Months { get; private set; }
        public double TotalWeeks { get; private set; }
        public int Weeks { get; private set; }
        public double TotalDays { get { return TimeSpan.TotalDays; } }
        public int Days { get; private set; }
        public double TotalHours { get { return TimeSpan.TotalHours; } }
        public int Hours { get { return TimeSpan.Hours; } }
        public double TotalMinutes { get { return TimeSpan.TotalMinutes; } }
        public int Minutes { get { return TimeSpan.Minutes; } }
        public double TotalSeconds { get { return TimeSpan.TotalSeconds; } }
        public int Seconds { get { return TimeSpan.Seconds; } }
        public double TotalMilliseconds { get { return TimeSpan.TotalMilliseconds; } }
        public int Milliseconds { get { return TimeSpan.Milliseconds; } }
        public TimeSpan TimeSpan { get; private set; }

        public override string ToString() {
            return ToString(Int32.MaxValue, false);
        }

        public string ToString(int maxParts, bool shortForm = false, bool includeMilliseconds = false) {
            int partCount = 0;
            if (maxParts <= 0)
                maxParts = Int32.MaxValue;
            var sb = new StringBuilder();

            if (AppendPart(sb, "year", Years, shortForm, ref partCount))
                if (maxParts > 0 && partCount >= maxParts)
                    return sb.ToString();

            if (AppendPart(sb, "month", Months, shortForm, ref partCount))
                if (maxParts > 0 && partCount >= maxParts)
                    return sb.ToString();

            if (AppendPart(sb, "week", Weeks, shortForm, ref partCount))
                if (maxParts > 0 && partCount >= maxParts)
                    return sb.ToString();

            if (AppendPart(sb, "day", Days, shortForm, ref partCount))
                if (maxParts > 0 && partCount >= maxParts)
                    return sb.ToString();

            if (AppendPart(sb, "hour", Hours, shortForm, ref partCount))
                if (maxParts > 0 && partCount >= maxParts)
                    return sb.ToString();

            if (AppendPart(sb, "minute", Minutes, shortForm, ref partCount))
                if (maxParts > 0 && partCount >= maxParts)
                    return sb.ToString();

            double seconds = includeMilliseconds || Math.Abs(TotalMinutes) > 1d ? Seconds : Math.Round(TotalSeconds, 2);
            if (seconds > 10)
                seconds = Math.Round(seconds);

            if (AppendPart(sb, "second", seconds, shortForm, ref partCount))
                if (maxParts > 0 && partCount >= maxParts)
                    return sb.ToString();

            if (includeMilliseconds && AppendPart(sb, "millisecond", Milliseconds, shortForm, ref partCount))
                if (maxParts > 0 && partCount >= maxParts)
                    return sb.ToString();

            return sb.ToString();
        }

        private static bool AppendPart(StringBuilder builder, string partName, double partValue, bool shortForm, ref int partCount) {
            const string spacer = " ";

            if (Math.Abs(partValue) == 0)
                return false;

            if (builder.Length > 0)
                builder.Append(spacer);

            string partValueString = partCount > 0 ? Math.Abs(partValue).ToString("0.##") : partValue.ToString("0.##");

            if (shortForm && partName == "millisecond")
                partName = "ms";
            else if (shortForm)
                partName = partName.Substring(0, 1);

            if (shortForm)
                builder.AppendFormat("{0}{1}", partValueString, partName);
            else
                builder.AppendFormat("{0} {1}{2}", partValueString, partName, GetTense(partValue));
            partCount++;

            return true;
        }

        private static string GetTense(double value) {
            return Math.Abs(value) > 1 ? "s" : String.Empty;
        }
    }
}
