using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Models.Stats {
    public class EventTermStatsItem {
        public EventTermStatsItem() {
            Timeline = new List<EventTermTimelineItem>();
        }

        public string Term { get; set; }
        public long Total { get; set; }
        public long Unique { get; set; }
        public long New { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public ICollection<EventTermTimelineItem> Timeline { get; private set; }
    }
}