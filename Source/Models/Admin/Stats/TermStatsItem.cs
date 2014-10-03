using System;
using System.Collections.Generic;

namespace Exceptionless.Models.Stats {
    public class TermStatsItem {
        public TermStatsItem() {
            Timeline = new List<TermTimelineItem>();
        }

        public string Term { get; set; }
        public long Total { get; set; }
        public long Unique { get; set; }
        public long New { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public ICollection<TermTimelineItem> Timeline { get; private set; }
    }
}