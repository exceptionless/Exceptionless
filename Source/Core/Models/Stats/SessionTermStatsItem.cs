using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Models.Stats {
    public class SessionTermStatsItem {
        public SessionTermStatsItem() {
            Timeline = new List<SessionTermTimelineItem>();
        }

        public string Term { get; set; }
        public long Sessions { get; set; }
        public long Users { get; set; }
        public decimal AvgDuration { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public ICollection<SessionTermTimelineItem> Timeline { get; private set; }
    }
}