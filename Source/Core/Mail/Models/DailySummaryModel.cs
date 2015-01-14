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
using Exceptionless.Models;

namespace Exceptionless.Core.Mail.Models {
    public class DailySummaryModel : IMailModel {
        public string Subject { get; set; }
        public string BaseUrl { get; set; }

        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public bool HasSubmittedEvents { get; set; }
        public bool IsFreePlan { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public long Total { get; set; }
        public long UniqueTotal { get; set; }
        public long NewTotal { get; set; }
        public double PerHourAverage { get; set; }
        public List<Stack> New { get; set; }
        public List<EventStackResult> MostFrequent { get; set; }
    }
}