using Newtonsoft.Json;

namespace Exceptionless.Core.Models;

public record SlackToken
{
    public string AccessToken { get; init; } = null!;
    public string[] Scopes { get; init; } = null!;
    public string UserId { get; init; } = null!;
    public string TeamId { get; init; } = null!;
    public string TeamName { get; init; } = null!;
    public IncomingWebHook? IncomingWebhook { get; set; }

    public record IncomingWebHook
    {
        public string Channel { get; init; } = null!;
        public string ChannelId { get; init; } = null!;
        public string ConfigurationUrl { get; init; } = null!;
        public string Url { get; init; } = null!;
    }
}

public record SlackMessage
{
    public SlackMessage(string text)
    {
        Text = text;
    }

    [JsonProperty("text")]
    public string Text { get; init; }
    [JsonProperty("attachments")]
    public List<SlackAttachment> Attachments { get; init; } = new();

    public class SlackAttachment
    {
        public SlackAttachment(PersistentEvent ev)
        {
            TimeStamp = ev.Date.ToUnixTimeSeconds();

            var ud = ev.GetUserDescription();
            var ui = ev.GetUserIdentity();
            Text = ud?.Description;

            string? displayName = null;
            if (!String.IsNullOrEmpty(ui?.Identity))
                displayName = ui.Identity;

            if (!String.IsNullOrEmpty(ui?.Name))
                displayName = ui.Name;

            if (!String.IsNullOrEmpty(displayName) && !String.IsNullOrEmpty(ud?.EmailAddress))
                displayName = $"{displayName} ({ud.EmailAddress})";
            else if (!String.IsNullOrEmpty(ui?.Identity) && !String.IsNullOrEmpty(ui.Name))
                displayName = $"{ui.Name} ({ui.Identity})";

            if (!String.IsNullOrEmpty(displayName))
            {
                AuthorName = displayName;

                if (!String.IsNullOrEmpty(ud?.EmailAddress))
                {
                    AuthorLink = $"mailto:{ud.EmailAddress}?body={ud.Description}";
                    //AuthorIcon = $"https://www.gravatar.com/avatar/{ud.EmailAddress.ToMD5()}",
                }
            }
        }

        [JsonProperty("title")]
        public string? Title { get; init; }
        [JsonProperty("text")]
        public string? Text { get; init; }
        [JsonProperty("author_name")]
        public string? AuthorName { get; init; }
        [JsonProperty("author_link")]
        public string? AuthorLink { get; init; }
        [JsonProperty("author_icon")]
        public string? AuthorIcon { get; init; }
        [JsonProperty("color")]
        public string Color { get; set; } = "#5E9A00";
        [JsonProperty("fields")]
        public List<SlackAttachmentFields> Fields { get; init; } = new();
        [JsonProperty("mrkdwn_in")]
        public string[] SupportedMarkdownFields { get; init; } = { "text", "fields" };
        [JsonProperty("ts")]
        public long TimeStamp { get; init; }
    }

    public record SlackAttachmentFields
    {
        [JsonProperty("title")]
        public string Title { get; init; } = null!;

        [JsonProperty("value")]
        public string? Value { get; init; }
        [JsonProperty("short")]
        public bool Short { get; init; }
    }
}
