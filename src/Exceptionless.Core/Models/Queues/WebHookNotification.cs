using System;

namespace Exceptionless.Core.Queues.Models {
    public class WebHookNotification {
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string WebHookId { get; set; }
        public WebHookType Type { get; set; } = WebHookType.General;
        public string Url { get; set; }
        public object Data { get; set; }
    }

    public enum WebHookType {
        General = 0,
        Slack = 1
    }
}