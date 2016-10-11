using System;

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
        public double UniqueTotal { get; set; }
        public double NewTotal { get; set; }
        public double PerHourAverage { get; set; }
    }
}