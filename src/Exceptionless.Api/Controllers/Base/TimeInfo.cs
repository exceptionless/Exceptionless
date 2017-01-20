using System;
using System.Diagnostics;
using Exceptionless.DateTimeExtensions;
using Foundatio.Utility;

namespace Exceptionless.Api.Controllers {
    [DebuggerDisplay("Range: {Range} Offset: {Offset} Field: {Field}")]
    public class TimeInfo {
        public string Field { get; set; }
        public DateTimeRange Range { get; set; }
        public TimeSpan Offset { get; set; }
    }

    public static class TimeInfoExtensions {
        public static TimeInfo ApplyMinimumUtcStartDate(this TimeInfo ti, DateTime minimumUtcStartDate) {
            if (ti.Range.UtcStart < minimumUtcStartDate) {
                long startTicks = minimumUtcStartDate.Ticks + ti.Offset.Ticks;
                var start = startTicks > DateTime.MinValue.Ticks ? new DateTimeOffset(startTicks, ti.Offset) : new DateTimeOffset(DateTime.MinValue, TimeSpan.Zero);

                long endTicks = ti.Range.UtcEnd.Ticks + ti.Offset.Ticks;
                var end = ti.Range.UtcEnd < DateTime.MaxValue && endTicks < DateTime.MaxValue.Ticks ? new DateTimeOffset(endTicks, ti.Offset) : new DateTimeOffset(DateTime.MaxValue, TimeSpan.Zero);
                ti.Range = new DateTimeRange(start, end);
            }

            return ti;
        }

        public static TimeInfo AdjustEndTimeIfMaxValue(this TimeInfo ti) {
            if (ti.Range.UtcEnd == DateTime.MaxValue) {
                long startTicks = ti.Range.UtcStart.Ticks + ti.Offset.Ticks;
                var start = startTicks > DateTime.MinValue.Ticks ? new DateTimeOffset(startTicks, ti.Offset) : new DateTimeOffset(DateTime.MinValue, TimeSpan.Zero);

                var end = new DateTimeOffset(SystemClock.UtcNow.Ticks + ti.Offset.Ticks, ti.Offset);
                ti.Range = new DateTimeRange(start, end);
            }

            return ti;
        }
    }
}