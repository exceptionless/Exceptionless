using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Models.Stats {
    public class EventTermStatsResult {
        public EventTermStatsResult() {
            Terms = new List<TermStatsItem>();
        }

        public long Total { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public ICollection<TermStatsItem> Terms { get; private set; }
        public long Unique { get; set; }
        public long New { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
    }
}