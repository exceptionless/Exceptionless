using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Models.Stats {
    public class NumbersTimelineStatsResult : NumbersStatsResult {
        public new static NumbersTimelineStatsResult Empty { get; } = new NumbersTimelineStatsResult();

        public NumbersTimelineStatsResult() {
            Timeline = new List<NumbersTimelineItem>();
        }

        public ICollection<NumbersTimelineItem> Timeline { get; private set; }
        public double AvgPerHour { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
    }
}