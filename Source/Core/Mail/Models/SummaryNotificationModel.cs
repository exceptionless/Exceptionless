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
    public class SummaryNotificationModel : IMailModel {
        public string Subject { get; set; }
        public string BaseUrl { get; set; }

        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public bool HasSubmittedErrors { get; set; }
        public bool IsFreePlan { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public int Total { get; set; }
        public int UniqueTotal { get; set; }
        public int NewTotal { get; set; }
        public double PerHourAverage { get; set; }
        public List<ErrorStack> New { get; set; }
        public List<ErrorStackResult> MostFrequent { get; set; }
    }
}