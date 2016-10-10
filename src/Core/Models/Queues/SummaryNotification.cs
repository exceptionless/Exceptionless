using System;

namespace Exceptionless.Core.Queues.Models {
    public class SummaryNotification {
        public string Id { get; set; }
        public DateTime UtcStartTime { get; set; }
        public DateTime UtcEndTime { get; set; }
    }
}