using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Models.Stats {
    public class SessionTermStatsResult {
        public SessionTermStatsResult() {
            Terms = new List<SessionTermStatsItem>();
        }
        
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public long Sessions { get; set; }
        public long Users { get; set; }
        public decimal AvgDuration { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public ICollection<SessionTermStatsItem> Terms { get; private set; }
    }
}