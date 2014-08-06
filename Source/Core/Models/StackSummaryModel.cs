using System;

namespace Exceptionless.Core.Models {
    public class StackSummaryModel : SummaryData {
        public StackSummaryModel(string id, string title, DateTime firstOccurrence, DateTime lastOccurrence, SummaryData data) : base(data.TemplateKey, data.Data) {
            Id = id;
            Title = title;
            FirstOccurrence = firstOccurrence;
            LastOccurrence = lastOccurrence;
        }

        public string Id { get; private set; }
        public string Title { get; set; }
        public DateTime FirstOccurrence { get; private set; }
        public DateTime LastOccurrence { get; private set; }
    }
}