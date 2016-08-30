using System;

namespace Exceptionless.Core.Models.Stats {
    public class NumbersStatsResult {
        public static NumbersStatsResult Empty { get; } = new NumbersStatsResult();

        public long Total { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public double[] Numbers { get; set; }
    }
}