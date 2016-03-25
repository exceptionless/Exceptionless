using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Models.Stats {
    public class NumbersTermStatsResult : NumbersStatsResult {
        public NumbersTermStatsResult() {
            Terms = new List<NumbersTermStatsItem>();
        }

        public ICollection<NumbersTermStatsItem> Terms { get; private set; }
    }
}