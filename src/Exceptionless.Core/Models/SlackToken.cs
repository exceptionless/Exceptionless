using System.Text.Json.Serialization;
using Foundatio.Serializer;

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

    [JsonPropertyName("text")]
    public string Text { get; init; }
    [JsonPropertyName("attachments")]
    public List<SlackAttachment> Attachments { get; init; } = [];

    public class SlackAttachment
    {
        public SlackAttachment(PersistentEvent ev, ITextSerializer serializer)
        {
            TimeStamp = ev.Date.ToUnixTimeSeconds();

            var ud = ev.GetUserDescription(serializer);
            var ui = ev.GetUserIdentity(serializer);
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

        [JsonPropertyName("title")]
        public string? Title { get; init; }
        [JsonPropertyName("text")]
        public string? Text { get; init; }
        [JsonPropertyName("author_name")]
        public string? AuthorName { get; init; }
        [JsonPropertyName("author_link")]
        public string? AuthorLink { get; init; }
        [JsonPropertyName("author_icon")]
        public string? AuthorIcon { get; init; }
        [JsonPropertyName("color")]
        public string Color { get; set; } = "#5E9A00";
        [JsonPropertyName("fields")]
        public List<SlackAttachmentFields> Fields { get; init; } = [];
        [JsonPropertyName("mrkdwn_in")]
        public string[] SupportedMarkdownFields { get; init; } = ["text", "fields"];
        [JsonPropertyName("ts")]
        public long TimeStamp { get; init; }
    }

    public record SlackAttachmentFields
    {
        [JsonPropertyName("title")]
        public string Title { get; init; } = null!;

        [JsonPropertyName("value")]
        public string? Value { get; init; }
        [JsonPropertyName("short")]
        public bool Short { get; init; }
    }
}
