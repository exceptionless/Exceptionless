using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Models.Stats {
    public class SessionStatsResult {
        public SessionStatsResult() {
            Timeline = new List<SessionTimelineItem>();
        }

        public long Sessions { get; set; }
        public long Users { get; set; }
        public decimal AvgDuration { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public double AvgPerHour { get; set; }
        public ICollection<SessionTimelineItem> Timeline { get; private set; }
    }
}