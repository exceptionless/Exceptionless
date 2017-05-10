using System;

namespace Exceptionless.Core.Models {
    public class SlackToken {
        public string AccessToken { get; set; }
        public string[] Scopes { get; set; }
        public string UserId { get; set; }
        public string TeamId { get; set; }
        public string TeamName { get; set; }
        public IncomingWebHook IncomingWebhook { get; set; }

        public class IncomingWebHook {
            public string Channel { get; set; }
            public string ChannelId { get; set; }
            public string ConfigurationUrl { get; set; }
            public string Url { get; set; }
        }
    }
}