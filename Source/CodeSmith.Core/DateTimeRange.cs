using System;
using System.Text.RegularExpressions;
using System.Web.Configuration;
using CodeSmith.Core.Extensions;

namespace CodeSmith.Core {
    public class DateTimeRange : IEquatable<DateTimeRange>, IComparable<DateTimeRange> {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }

        public DateTime StartUtc { get { return Start.ToUniversalTime(); } }
        public DateTime EndUtc { get { return End.ToUniversalTime(); } }

        public const string DefaultSeparator = " - ";

        public DateTimeRange(DateTime start, DateTime end) {
            Start = start;
            End = end;
        }

        public static bool operator ==(DateTimeRange left, DateTimeRange right) {
            if (left == null || right == null)
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(DateTimeRange left, DateTimeRange right) {
            if (left == null || right == null)
                return true;

            return !left.Equals(right);
        }

        public override bool Equals(object obj) {
            if (obj == null)
                return false;

            var other = obj as DateTimeRange;
            return other != null && Equals(other);
        }

        public override int GetHashCode() {
            return (Start.Ticks + End.Ticks).GetHashCode();
        }

        public override string ToString() {
            return ToString(DefaultSeparator);
        }

        public string ToString(string separator) {
            return Start + separator + End;
        }

        public string ToShortDateString(string separator = DefaultSeparator) {
            return String.Concat(Start.ToShortDateString(), separator, End.ToShortDateString());
        }

        public bool Equals(DateTimeRange other) {
            return Start.Equals(other.Start) && End.Equals(other.End);
        }

        public int CompareTo(DateTimeRange other) {
            if (other == null)
                return 1;

            if (Equals(other))
                return 0;

            return Start.CompareTo(other.End);
        }

        private static readonly Regex _parser = new Regex(@"
^\s*(?:
  (?:(?<opt1>today|yesterday|tomorrow))
|
  (?:(?<opt2>this|past|last|next|previous)\s+(?<arg1>\d+)\s+(?<arg2>minutes|minute|hours|hour|days|day|weeks|week|months|month|years|year))
|
  (?:(?<opt3>this|past|last|next|previous)\s+(?<arg1>minute|hour|day|week|month|year))
|
  (?:(?<opt4>this|past|last|next|previous)\s+(?<arg1>january|jan|february|feb|march|mar|april|apr|may|june|jun|july|jul|august|aug|september|sep|october|oct|november|nov|december|dec))
|
  (?:(?<opt5>january|jan|february|feb|march|mar|april|apr|may|june|jun|july|jul|august|aug|september|sep|october|oct|november|nov|december|dec))
|
  (?<value1>
      (?:(?<val1opt1>now|today|yesterday|tomorrow))
    |
      (?:(?<val1opt2>\d+)\s+(?<arg1>minutes|minute|hours|hour|days|day|weeks|week|months|month|years|year)\s+(?<arg2>ago|from now))
    |
      (?:(?<val1opt3>a)\s+(?<arg1>minute|hour|day|week|month|year)\s+(?<arg2>ago|from now))
    |
      (?:(?<val1opt4>this|last|next)\s+(?<arg1>january|jan|february|feb|march|mar|april|apr|may|june|jun|july|jul|august|aug|september|sep|october|oct|november|nov|december|dec))
    |
      (?:(?<val1opt5>january|jan|february|feb|march|mar|april|apr|may|june|jun|july|jul|august|aug|september|sep|october|oct|november|nov|december|dec))
    |
      (?<val1opt6>\d{4}-\d{2}-\d{2}(?:T\d{2}\:\d{2})?(?:\:\d{2})?)
  )\s+TO\s+(?<value2>
      (?:(?<val2opt1>now|today|yesterday|tomorrow))
    |
      (?:(?<val2opt2>\d+)\s+(?<arg1>minutes|minute|hours|hour|days|day|weeks|week|months|month|years|year)\s+(?<arg2>ago|from now))
    |
      (?:(?<val2opt3>a)\s+(?<arg1>minute|hour|day|week|month|year)\s+(?<arg2>ago|from now))
    |
      (?:(?<val2opt4>this|last|next)\s+(?<arg1>january|jan|february|feb|march|mar|april|apr|may|june|jun|july|jul|august|aug|september|sep|october|oct|november|nov|december|dec))
    |
      (?:(?<val2opt5>january|jan|february|feb|march|mar|april|apr|may|june|jun|july|jul|august|aug|september|sep|october|oct|november|nov|december|dec))
    |
      (?<val2opt6>\d{4}-\d{2}-\d{2}(?:T\d{2}\:\d{2})?(?:\:\d{2})?)
  )
)\s*$
                ", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase);

        public static DateTimeRange Parse(string range) {
            return Parse(range, DateTime.Now);
        }

        public static DateTimeRange Parse(string range, DateTime now) {
            var m = _parser.Match(range);
            // store now to a variable so that all relative calculations are based on the same time
            
            // is it a single range?
            if (!m.Groups["value1"].Success) {
                if (m.Groups["opt1"].Success) {
                    string value = m.Groups["opt1"].Value.ToLower();
                    if (value == "today")
                        return new DateTimeRange(now.Date, now.ToEndOfDay());
                    if (value == "yesterday")
                        return new DateTimeRange(now.Date.SubtractDays(1), now.Date.SubtractDays(1).ToEndOfDay());
                    if (value == "tomorrow")
                        return new DateTimeRange(now.Date.AddDays(1), now.Date.AddDays(1).ToEndOfDay());
                } else if (m.Groups["opt2"].Success) {
                    return FromRelationAndAmount(m.Groups["opt2"].Value, Int32.Parse(m.Groups["arg1"].Value), m.Groups["arg2"].Value, now);
                } else if (m.Groups["opt3"].Success) {
                    return FromRelationAndAmount(m.Groups["opt3"].Value, 1, m.Groups["arg1"].Value, now);
                } else if (m.Groups["opt4"].Success) {
                    string relation = m.Groups["opt4"].Value.ToLower();
                    int month = MonthNumberFromName(m.Groups["arg1"].Value);
                    return FromRelationAndMonth(relation, month, now);
                } else if (m.Groups["opt5"].Success) {
                    int month = MonthNumberFromName(m.Groups["opt5"].Value);
                    return FromRelationAndMonth("this", month, now);
                }
            }
            
            return null;
        }

        private static DateTimeRange FromRelationAndMonth(string relation, int month, DateTime now) {
            switch (relation) {
                case "this":
                    var start = now.Month == month ? now.ChangeMonth(month).ToBeginningOfMonth() : now.AddYears(1).ChangeMonth(month).ToBeginningOfMonth();
                    return new DateTimeRange(start, start.ToEndOfMonth());
                case "last":
                case "past":
                case "previous":
                    return new DateTimeRange(now.SubtractYears(1).ChangeMonth(month).ToBeginningOfMonth(), now.SubtractYears(1).ChangeMonth(month).ToEndOfMonth());
                case "next":
                    return new DateTimeRange(now.AddYears(1).ChangeMonth(month).ToBeginningOfMonth(), now.AddYears(1).ChangeMonth(month).ToEndOfMonth());
            }

            return null;
        }

        private static DateTimeRange FromRelationAndAmount(string relation, int amount, string time, DateTime now) {
            relation = relation.ToLower();
            time = time.ToLower();
            if (amount < 1)
                throw new ArgumentException("Time amount can't be 0.");
            TimeSpan timeSpan = FromName(time);

            if (timeSpan != TimeSpan.Zero) {
                switch (relation) {
                    case "this":
                        return new DateTimeRange(now.Floor(timeSpan).Subtract(TimeSpan.FromTicks(timeSpan.Ticks * (amount - 1))), now);
                    case "last":
                    case "past":
                    case "previous":
                        return new DateTimeRange(now.Floor(timeSpan).Subtract(TimeSpan.FromTicks(timeSpan.Ticks * amount)), now.Floor(timeSpan));
                    case "next":
                        return new DateTimeRange(now.Floor(timeSpan), now.Ceiling(timeSpan).Add(TimeSpan.FromTicks(timeSpan.Ticks * (amount - 1))));
                }
            } else if (time == "month" || time == "months") {
                switch (relation) {
                    case "this":
                        return new DateTimeRange(now.ToBeginningOfMonth().SubtractMonths(amount - 1), now);
                    case "last":
                    case "past":
                    case "previous":
                        return new DateTimeRange(now.ToBeginningOfMonth().SubtractMonths(amount), now.ToBeginningOfMonth());
                    case "next":
                        return new DateTimeRange(now.ToBeginningOfMonth(), now.ToEndOfMonth().AddMonths(amount - 1));
                }
            } else if (time == "year" || time == "years") {
                switch (relation) {
                    case "this":
                        return new DateTimeRange(now.ToBeginningOfYear().SubtractYears(amount - 1), now);
                    case "last":
                    case "past":
                    case "previous":
                        return new DateTimeRange(now.ToBeginningOfYear().SubtractYears(amount), now.ToBeginningOfYear());
                    case "next":
                        return new DateTimeRange(now.ToBeginningOfYear(), now.ToEndOfYear().AddYears(amount - 1));
                }
            }

            return null;
        }

        private static TimeSpan FromName(string name) {
            switch (name.ToLower()) {
                case "minutes":
                case "minute":
                    return TimeSpan.FromMinutes(1);
                case "hours":
                case "hour":
                    return TimeSpan.FromHours(1);
                case "days":
                case "day":
                    return TimeSpan.FromDays(1);
                case "weeks":
                case "week":
                    return TimeSpan.FromDays(7);
                default:
                    return TimeSpan.Zero;
            }
        }

        private static int MonthNumberFromName(string name) {
            switch (name.ToLower()) {
                case "jan":
                case "january":
                    return 1;
                case "feb":
                case "february":
                    return 2;
                case "mar":
                case "march":
                    return 3;
                case "apr":
                case "april":
                    return 4;
                case "may":
                    return 5;
                case "jun":
                case "june":
                    return 6;
                case "jul":
                case "july":
                    return 7;
                case "aug":
                case "august":
                    return 8;
                case "sep":
                case "september":
                    return 9;
                case "oct":
                case "october":
                    return 10;
                case "nov":
                case "november":
                    return 11;
                case "dec":
                case "december":
                    return 12;
            }

            return -1;
        }
    }
}