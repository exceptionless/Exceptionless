using System;

namespace Exceptionless.Core.Models.Stats {
    public class NumbersTermStatsItem {
        public string Term { get; set; }
        public long Total { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public double[] Numbers { get; set; }
    }
}