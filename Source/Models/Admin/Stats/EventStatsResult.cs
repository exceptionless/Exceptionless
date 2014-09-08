using System;
using System.Collections.Generic;

namespace Exceptionless.Models.Stats {
    public class EventStatsResult {
        public EventStatsResult() {
            Timeline = new List<TimelineItem>();
        }

        public long Total { get; set; }
        public long Unique { get; set; }
        public long New { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public double AvgPerHour { get; set; }
        public ICollection<TimelineItem> Timeline { get; private set; }
    }
}