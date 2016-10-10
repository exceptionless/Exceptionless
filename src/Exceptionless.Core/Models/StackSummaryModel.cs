using System;

namespace Exceptionless.Core.Models {
    public class StackSummaryModel : SummaryData {
        public string Id { get; set; }
        public string Title { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public long Total { get; set; }

        public double Users { get; set; }
        public double TotalUsers { get; set; }
    }
}