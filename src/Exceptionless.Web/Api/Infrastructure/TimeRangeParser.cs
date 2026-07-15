using Exceptionless.Core.Extensions;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Controllers;

namespace Exceptionless.Web.Api.Infrastructure;

public static class TimeRangeParser
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
        var range = DateTimeRange.Parse(time, timeProvider.GetUtcNow().ToOffset(utcOffset));
        var timeInfo = new TimeInfo { Field = field, Offset = utcOffset, Range = range };
        if (minimumUtcStartDate.HasValue)
            timeInfo.ApplyMinimumUtcStartDate(minimumUtcStartDate.Value);

        timeInfo.AdjustEndTimeIfMaxValue(timeProvider);
        return timeInfo;
    }
}
