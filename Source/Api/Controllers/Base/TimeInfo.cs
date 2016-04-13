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
        public static TimeInfo ApplyMinimumUtcStartDate(this TimeInfo timeInfo, DateTime minimumUtcStartDate) {
            if (timeInfo.UtcRange.UtcStart < minimumUtcStartDate)
                timeInfo.UtcRange = new DateTimeRange(minimumUtcStartDate.SafeAdd(timeInfo.Offset), timeInfo.UtcRange.End);
            
            return timeInfo;
        }
    } 
}