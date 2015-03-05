using System;
using System.Linq;
using Exceptionless.Core.Queues.Models;

namespace Exceptionless.Core.Mail.Models {
    public class EventNotificationModel : EventNotification, IMailModel {
        public EventNotificationModel(EventNotification notification) {
            Event = notification.Event;
            ProjectName = notification.ProjectName;
            IsNew = notification.IsNew;
            IsCritical = notification.IsCritical;
            IsRegression = notification.IsRegression;
            TotalOccurrences = notification.TotalOccurrences;
        }

        public string BaseUrl { get; set; }
        public string Url { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }

        public string MethodFullName { get; set; }
        public string TypeFullName { get; set; }
        public string TypeName {
            get {
                return !String.IsNullOrEmpty(TypeFullName) ? TypeFullName.Split('.').Last() : String.Empty;
            }
        }
    }
}