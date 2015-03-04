using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;

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