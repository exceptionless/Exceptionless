using System.Diagnostics;
using Exceptionless.DateTimeExtensions;

namespace Exceptionless.Web.Controllers;

[DebuggerDisplay("Range: {Range} Offset: {Offset} Field: {Field}")]
public record TimeInfo
{
    public required string Field { get; set; }
    public required DateTimeRange Range { get; set; }
    public required TimeSpan Offset { get; set; }
}

public static class TimeInfoExtensions
{
    public static void ApplyMinimumUtcStartDate(this TimeInfo ti, DateTime minimumUtcStartDate)
    {
        if (ti.Range.UtcStart >= minimumUtcStartDate)
            return;

        long startTicks = minimumUtcStartDate.Ticks + ti.Offset.Ticks;
        var start = startTicks > DateTime.MinValue.Ticks ? new DateTimeOffset(startTicks, ti.Offset) : new DateTimeOffset(DateTime.MinValue, TimeSpan.Zero);

        long endTicks = ti.Range.UtcEnd.Ticks + ti.Offset.Ticks;
        var end = ti.Range.UtcEnd < DateTime.MaxValue && endTicks < DateTime.MaxValue.Ticks ? new DateTimeOffset(endTicks, ti.Offset) : new DateTimeOffset(DateTime.MaxValue, TimeSpan.Zero);
        ti.Range = new DateTimeRange(start, end);
    }

    public static void AdjustEndTimeIfMaxValue(this TimeInfo ti)
    {
        if (ti.Range.UtcEnd != DateTime.MaxValue)
            return;

        long startTicks = ti.Range.UtcStart.Ticks + ti.Offset.Ticks;
        var start = startTicks > DateTime.MinValue.Ticks ? new DateTimeOffset(startTicks, ti.Offset) : new DateTimeOffset(DateTime.MinValue, TimeSpan.Zero);

        var end = new DateTimeOffset(_timeProvider.GetUtcNow().UtcDateTime.Ticks + ti.Offset.Ticks, ti.Offset);
        ti.Range = new DateTimeRange(start, end);
    }
}
