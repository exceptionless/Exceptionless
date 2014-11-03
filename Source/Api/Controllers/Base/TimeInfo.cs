using System;
using CodeSmith.Core;

namespace Exceptionless.Api.Controllers {
    public class TimeInfo {
        public string Field { get; set; }
        public DateTimeRange Range { get; set; }
        public TimeSpan Offset { get; set; }
    }
}