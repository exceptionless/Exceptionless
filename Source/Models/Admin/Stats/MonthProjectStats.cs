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
using System.Globalization;

namespace Exceptionless.Models {
    public class MonthProjectStats : EventStatsWithStackIds, IIdentity, IOwnedByProject {
        public MonthProjectStats() {
            DayStats = new Dictionary<string, EventStatsWithStackIds>();
        }

        public string Id { get; set; }
        public string ProjectId { get; set; }
        public Dictionary<string, EventStatsWithStackIds> DayStats { get; set; }

        public DateTime GetDateFromDayStatKey(string day) {
            const string format = "yyyyMMd";

            // NOTE: We use the index of as the offset can change the strings length.
            // 5162a63e7841550da8110884/02:00/201201
            // 5111686f772d9409a87d2870/-06:00/201302
            string date = String.Concat(Id.Substring(Id.IndexOf('/', 25) + 1), day);
            DateTime result;
            if (!DateTime.TryParseExact(date, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result))
                throw new FormatException(String.Format("Unable to parse \"{0}\" with format \"{1}\".", date, format));

            return result;
        }
    }
}