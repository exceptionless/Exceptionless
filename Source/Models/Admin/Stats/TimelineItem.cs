using System;

namespace Exceptionless.Models.Stats {
    public class TimelineItem {
        public DateTime Date { get; set; }
        public long Total { get; set; }
        public long Unique { get; set; }
        public long New { get; set; }
    }
}