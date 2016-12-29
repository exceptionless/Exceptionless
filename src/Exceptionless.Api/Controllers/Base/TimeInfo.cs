using System;
using System.Diagnostics;
using Exceptionless.DateTimeExtensions;

namespace Exceptionless.Api.Controllers {
    [DebuggerDisplay("Range: {UtcRange} Offset: {Offset} Field: {Field}")]
    public class TimeInfo {
        public string Field { get; set; }
        public DateTimeRange UtcRange { get; set; }
        public TimeSpan Offset { get; set; }
    }

    public static class TimeInfoExtensions {
        public static TimeInfo ApplyMinimumUtcStartDate(this TimeInfo ti, DateTime minimumUtcStartDate) {
            if (ti.UtcRange.UtcStart < minimumUtcStartDate)
                ti.UtcRange = new DateTimeRange(new DateTimeOffset(minimumUtcStartDate.Ticks + ti.Offset.Ticks, ti.Offset), new DateTimeOffset(ti.UtcRange.End, ti.Offset));

            return ti;
        }
    }
}