using System;

namespace Exceptionless.Core.Models {
    public class EventSummaryModel: SummaryData {
        public EventSummaryModel(string id, DateTimeOffset date, SummaryData data) : base(data.TemplateKey, data.Data) {
            Id = id;
            Date = date;
        }

        public string Id { get; private set; }
        public DateTimeOffset Date { get; private set; }
    }
}