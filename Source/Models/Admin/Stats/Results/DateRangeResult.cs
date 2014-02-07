#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;

namespace Exceptionless.Models.Stats {
    public class DateRangeResult<T> : TotalResultBase {
        public DateRangeResult() {
            Stats = new List<T>();
        }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public List<T> Stats { get; set; }

        public double PerDayAverage {
            get {
                int days = (EndDate - StartDate).Days;
                return days > 0 ? Math.Round((Total * 1.0) / days, 1) : Total;
            }
        }

        public override double PerHourAverage {
            get {
                double hours = (EndDate - StartDate).TotalHours;
                return hours > 0 ? Math.Round(Total / hours, 1) : 0;
            }
        }

        public virtual void ApplyTimeOffset(TimeSpan offset) {}
    }
}