using System;
using System.Collections.Generic;

namespace Exceptionless.Models.Stats {
    public class EventTermStatsResult {
        public EventTermStatsResult() {
            Terms = new List<TermStatsItem>();
        }

        public long Total { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public ICollection<TermStatsItem> Terms { get; private set; }
    }
}