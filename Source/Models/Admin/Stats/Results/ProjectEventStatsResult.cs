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
    public class ProjectEventStatsResult : DateRangeResult<DateProjectStatsResult> {
        public ProjectEventStatsResult(long totalLimitedByPlan = 0) {
            MostFrequent = new PlanPagedResult<ErrorStackResult>();
            MostRecent = new PlanPagedResult<ErrorStackResult>();

            TotalLimitedByPlan = totalLimitedByPlan;
        }

        public int UniqueTotal { get; set; }
        public int NewTotal { get; set; }

        public PlanPagedResult<ErrorStackResult> MostFrequent { get; set; }

        public PlanPagedResult<ErrorStackResult> MostRecent { get; set; }

        public long TotalLimitedByPlan { get; set; }

        public override void ApplyTimeOffset(TimeSpan offset) {
            StartDate = StartDate.Add(offset);
            EndDate = EndDate.Add(offset);

            //foreach (var r in MostFrequent.Results) {
            //    r.FirstOccurrence = r.FirstOccurrence.Add(offset);
            //    r.LastOccurrence = r.LastOccurrence.Add(offset);
            //}

            //foreach (var r in MostRecent.Results) {
            //    r.FirstOccurrence = r.FirstOccurrence.Add(offset);
            //    r.LastOccurrence = r.LastOccurrence.Add(offset);
            //}

            foreach (DateProjectStatsResult s in Stats)
                s.Date = s.Date.Add(offset);
        }
    }
}