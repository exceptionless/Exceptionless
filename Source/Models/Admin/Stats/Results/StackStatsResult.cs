#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

namespace Exceptionless.Models.Stats {
    public class StackStatsResult : DateRangeResult<DateStackStatsResult> {
        public StackStatsResult(long totalLimitedByPlan = 0) {
            TotalLimitedByPlan = totalLimitedByPlan;
        }

        public override void ApplyTimeOffset(TimeSpan offset) {
            StartDate = StartDate.Add(offset);
            EndDate = EndDate.Add(offset);

            foreach (DateStackStatsResult s in Stats)
                s.Date = s.Date.Add(offset);
        }

        public long TotalLimitedByPlan { get; set; }
    }
}