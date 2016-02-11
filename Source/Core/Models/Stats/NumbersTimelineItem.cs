using System;

namespace Exceptionless.Core.Models.Stats {
    public class NumbersTimelineItem {
        public DateTime Date { get; set; }
        public long Total { get; set; }
        public double[] Numbers { get; set; }
    }
}