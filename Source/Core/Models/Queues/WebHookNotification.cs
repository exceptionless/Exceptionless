using System;

namespace Exceptionless.Core.Queues.Models {
    public class WebHookNotification {
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string Url { get; set; }
        public object Data { get; set; }
    }
}