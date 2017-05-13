using System;
using System.Collections.Generic;
using Newtonsoft.Json;

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

    public class SlackMessage {
        public SlackMessage(string text) {
            Text = text;
        }

        [JsonProperty("text")]
        public string Text { get; set; }
        [JsonProperty("attachments")]
        public List<SlackAttachment> Attachments { get; set; } = new List<SlackAttachment>();

        public class SlackAttachment {
            public SlackAttachment(PersistentEvent ev) {
                TimeStamp = ev.Date.ToUnixTimeSeconds();

                var ud = ev.GetUserDescription();
                var ui = ev.GetUserIdentity();
                Text = ud?.Description;

                string displayName = null;
                if (!String.IsNullOrEmpty(ui?.Identity))
                    displayName = ui.Identity;

                if (!String.IsNullOrEmpty(ui?.Name))
                    displayName = ui.Name;

                if (!String.IsNullOrEmpty(displayName) && !String.IsNullOrEmpty(ud?.EmailAddress))
                    displayName = $"{displayName} ({ud.EmailAddress})";
                else if (!String.IsNullOrEmpty(ui?.Identity) && !String.IsNullOrEmpty(ui.Name))
                    displayName = $"{ui.Name} ({ui.Identity})";

                if (!String.IsNullOrEmpty(displayName)) {
                    AuthorName = displayName;

                    if (!String.IsNullOrEmpty(ud?.EmailAddress)) {
                        AuthorLink = $"mailto:{ud.EmailAddress}?body={ud.Description}";
                        //AuthorIcon = $"https://www.gravatar.com/avatar/{ud.EmailAddress.ToMD5()}",
                    }
                }
            }

            [JsonProperty("title")]
            public string Title { get; set; }
            [JsonProperty("text")]
            public string Text { get; set; }
            [JsonProperty("author_name")]
            public string AuthorName { get; set; }
            [JsonProperty("author_link")]
            public string AuthorLink { get; set; }
            [JsonProperty("author_icon")]
            public string AuthorIcon { get; set; }
            [JsonProperty("color")]
            public string Color { get; set; } = "#5E9A00";
            [JsonProperty("fields")]
            public List<SlackAttachmentFields> Fields { get; set; } = new List<SlackAttachmentFields>();
            [JsonProperty("mrkdwn_in")]
            public string[] SupportedMarkdownFields { get; set; } = { "text", "fields" };
            [JsonProperty("ts")]
            public long TimeStamp { get; set; }
        }

        public class SlackAttachmentFields {
            [JsonProperty("title")]
            public string Title { get; set; }
            [JsonProperty("value")]
            public string Value { get; set; }
            [JsonProperty("short")]
            public bool Short { get; set; }
        }
    }
}