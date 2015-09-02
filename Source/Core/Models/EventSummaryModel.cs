using System;

namespace Exceptionless.Core.Models {
    public class EventSummaryModel: SummaryData {
        public string Id { get; set; }
        public DateTimeOffset Date { get; set; }
    }
}