using System;
using System.Collections.Generic;
using Exceptionless.Models.Stats;

namespace Exceptionless.Core.Models {
    public class StackSummaryModel : SummaryData {
        public string Id { get; set; }
        public string Title { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }

        public long New { get; set; }
        public long Total { get; set; }
        public long Unique { get; set; }
        public ICollection<TermTimelineItem> Timeline { get; set; }
    }
}