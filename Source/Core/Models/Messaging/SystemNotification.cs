using System;

namespace Exceptionless.Core.Messaging.Models {
    public class SystemNotification {
        public DateTime Date { get; set; }
        public string Message { get; set; }
    }
}