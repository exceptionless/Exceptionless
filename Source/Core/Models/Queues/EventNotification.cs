using System;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Queues.Models {
    public class EventNotification : EventNotificationWorkItem {
        public EventNotification() {}

        public EventNotification(EventNotificationWorkItem workItem, PersistentEvent ev) {
            EventId = workItem.EventId;
            IsNew = workItem.IsNew;
            IsCritical = workItem.IsCritical;
            IsRegression = workItem.IsRegression;
            TotalOccurrences = workItem.TotalOccurrences;
            ProjectName = workItem.ProjectName;
            Event = ev;
        }

        public PersistentEvent Event { get; set; }
    }

    public class EventNotificationWorkItem {
        public string EventId { get; set; }
        public bool IsNew { get; set; }
        public bool IsCritical { get; set; }
        public bool IsRegression { get; set; }
        public int TotalOccurrences { get; set; }
        public string ProjectName { get; set; }
    }
}