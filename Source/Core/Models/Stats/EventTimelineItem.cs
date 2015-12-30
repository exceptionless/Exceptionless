using System;

namespace Exceptionless.Core.Models.Stats {
    public class EventTimelineItem {
        public DateTime Date { get; set; }
        public long Total { get; set; }
        public long Unique { get; set; }
        public long New { get; set; }
    }
}