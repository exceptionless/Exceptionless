using System;

namespace Exceptionless.Core.Messaging.Models {
    public class ReleaseNotification {
        public bool Critical { get; set; }
        public DateTime Date { get; set; }
        public string Message { get; set; }
    }
}