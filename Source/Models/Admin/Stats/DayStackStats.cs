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
    public class DayStackStats {
        public DayStackStats() {
            MinuteStats = new Dictionary<string, int>();
        }

        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string StackId { get; set; }
        public int Total { get; set; }
        public Dictionary<string, int> MinuteStats { get; set; }

        // TODO: see if we can share code between this and day project stats.
        public DateTime GetDateFromMinuteStatKey(string minute) {
            const string format = "yyyyMMdd";
            string date = String.Concat(Id.Substring(25, 4), Id.Substring(29, 2), Id.Substring(31, 2));
            DateTime result;
            if (!DateTime.TryParseExact(date, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result))
                throw new FormatException(String.Format("Unable to parse \"{0}\" with format \"{1}\".", date, format));

            double min;
            if (!Double.TryParse(minute, out min))
                throw new FormatException(String.Format("Unable to parse \"{0}\".", minute));
            result = result.AddMinutes(min);

            return result;
        }
    }
}