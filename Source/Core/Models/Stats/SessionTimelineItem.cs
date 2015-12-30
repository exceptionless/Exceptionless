using System;

namespace Exceptionless.Core.Models.Stats {
    public class SessionTimelineItem {
        public DateTime Date { get; set; }
        public long Sessions { get; set; }
        public long Users { get; set; }
        public decimal AvgDuration { get; set; }
    }
}