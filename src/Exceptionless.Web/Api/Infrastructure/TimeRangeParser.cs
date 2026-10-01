using System.Globalization;
using System.Text.RegularExpressions;
using Exceptionless.Core.Extensions;
using Exceptionless.DateTimeExtensions;

namespace Exceptionless.Web.Api.Infrastructure;

public static partial class TimeRangeParser
{
    private static readonly char[] TimeParts = ['|'];

    public static TimeSpan GetOffset(string? offset)
    {
        if (!String.IsNullOrEmpty(offset) && TimeUnit.TryParse(offset, out var value) && value.HasValue)
            return value.Value;

        return TimeSpan.Zero;
    }

    public static TimeInfo GetTimeInfo(string? time, string? offset, TimeProvider timeProvider, ICollection<string>? allowedDateFields = null, string defaultDateField = "created_utc", DateTime? minimumUtcStartDate = null)
    {
        string field = defaultDateField;
        if (!String.IsNullOrEmpty(time) && time.Contains('|'))
        {
            string[] parts = time.Split(TimeParts, StringSplitOptions.RemoveEmptyEntries);
            field = parts.Length > 0 && allowedDateFields?.Contains(parts[0]) == true ? parts[0] : defaultDateField;
            time = parts.Length > 1 ? parts[1] : null;
        }

        var utcOffset = GetOffset(offset);

        // range parsing needs to be based on the user's local time.
        var range = DateTimeRange.Parse(ExpandMonthBounds(time), timeProvider.GetUtcNow().ToOffset(utcOffset));
        var timeInfo = new TimeInfo { Field = field, Offset = utcOffset, Range = range };
        if (minimumUtcStartDate.HasValue)
            timeInfo.ApplyMinimumUtcStartDate(minimumUtcStartDate.Value);

        timeInfo.AdjustEndTimeIfMaxValue(timeProvider);
        return timeInfo;
    }

    private static string? ExpandMonthBounds(string? time)
    {
        if (String.IsNullOrWhiteSpace(time))
            return time;

        // DateTimeExtensions supports year/day bounds, but not yyyy-MM. Resolve month
        // bounds here so every API client uses the same calendar and offset semantics.
        if (DateTime.TryParseExact(time.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            time = $"[{time.Trim()} TO {time.Trim()}]";

        return MonthBoundRegex().Replace(time, match =>
        {
            if (!DateTime.TryParseExact(match.Groups["month"].Value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
                return match.Value;

            int day = match.Groups["upper"].Success ? DateTime.DaysInMonth(month.Year, month.Month) : 1;
            return $"{match.Groups["prefix"].Value}{month.ToString("yyyy-MM", CultureInfo.InvariantCulture)}-{day.ToString("D2", CultureInfo.InvariantCulture)}";
        });
    }

    [GeneratedRegex(@"(?<prefix>^\s*|[\[{]\s*|(?<upper>\bTO\s+))(?<month>[0-9]{4}-[0-9]{2})(?=\s*(?:\bTO\b|[\]}]|$))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MonthBoundRegex();
}
