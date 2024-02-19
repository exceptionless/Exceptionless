using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Queues.Models;
using Foundatio.Queues;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;
// ReSharper disable InconsistentNaming

namespace Exceptionless.Core.Services;

public class SlackService
{
    private readonly HttpClient _client = new();
    private readonly IQueue<WebHookNotification> _webHookNotificationQueue;
    private readonly FormattingPluginManager _pluginManager;
    private readonly ISerializer _serializer;
    private readonly AppOptions _appOptions;
    private readonly ILogger _logger;

    public SlackService(IQueue<WebHookNotification> webHookNotificationQueue, FormattingPluginManager pluginManager, ITextSerializer serializer, AppOptions appOptions, ILoggerFactory loggerFactory)
    {
        _webHookNotificationQueue = webHookNotificationQueue;
        _pluginManager = pluginManager;
        _serializer = serializer;
        _appOptions = appOptions;
        _logger = loggerFactory.CreateLogger<SlackService>();
    }

    public async Task<SlackToken?> GetAccessTokenAsync(string code)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);

        if (String.IsNullOrEmpty(_appOptions.SlackOptions.SlackId) || String.IsNullOrEmpty(_appOptions.SlackOptions.SlackSecret))
            throw new Exception("SlackId or SlackSecret requires configuration");

        var data = new Dictionary<string, string> {
                { "client_id", _appOptions.SlackOptions.SlackId },
                { "client_secret", _appOptions.SlackOptions.SlackSecret },
                { "code", code },
                { "redirect_uri", new Uri(_appOptions.BaseURL).GetLeftPart(UriPartial.Authority) }
            };

        string url = $"https://slack.com/api/oauth.access?{data!.ToQueryString()}";
        var response = await _client.PostAsync(url);
        byte[] body = await response.Content.ReadAsByteArrayAsync();
        var result = _serializer.Deserialize<OAuthAccessResponse>(body);

        if (!result.ok)
        {
            _logger.LogWarning("Error getting access token: {Message}, Response: {Response}", result.error ?? result.warning, result);
            return null;
        }

        var token = new SlackToken
        {
            AccessToken = result.access_token!,
            Scopes = result.scope?.Split([","], StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>(),
            UserId = result.user_id!,
            TeamId = result.team_id!,
            TeamName = result.team_name!
        };

        if (result.incoming_webhook is not null)
        {
            token.IncomingWebhook = new SlackToken.IncomingWebHook
            {
                Channel = result.incoming_webhook.channel,
                ChannelId = result.incoming_webhook.channel_id,
                ConfigurationUrl = result.incoming_webhook.configuration_url,
                Url = result.incoming_webhook.url
            };
        }

        return token;
    }

    public async Task<bool> RevokeAccessTokenAsync(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        string url = $"https://slack.com/api/auth.revoke?token={token}";
        var response = await _client.PostAsync(url);
        byte[] body = await response.Content.ReadAsByteArrayAsync();
        var result = _serializer.Deserialize<AuthRevokeResponse>(body);

        if (result.ok && result.revoked || String.Equals(result.error, "invalid_auth"))
            return true;

        _logger.LogWarning("Error revoking token: {Message}, Response: {Response}", result.error ?? result.warning, result);
        return false;
    }

    public Task SendMessageAsync(string organizationId, string projectId, string url, SlackMessage message)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        ArgumentException.ThrowIfNullOrEmpty(url);
        ArgumentNullException.ThrowIfNull(message);

        var notification = new WebHookNotification
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            Url = url,
            Type = WebHookType.Slack,
            Data = message
        };

        return _webHookNotificationQueue.EnqueueAsync(notification);
    }

    public async Task<bool> SendEventNoticeAsync(PersistentEvent ev, Project project, bool isNew, bool isRegression)
    {
        var token = project.GetSlackToken();
        if (token?.IncomingWebhook?.Url is null)
            return false;

        bool isCritical = ev.IsCritical();
        var message = _pluginManager.GetSlackEventNotificationMessage(ev, project, isCritical, isNew, isRegression);
        if (message is null)
        {
            _logger.LogWarning("Unable to create event notification slack message for event {Id}", ev.Id);
            return false;
        }

        await SendMessageAsync(ev.OrganizationId, ev.ProjectId, token.IncomingWebhook.Url, message);
        return true;
    }

    private record Response
    {
        public bool ok { get; init; }
        public string? warning { get; init; }
        public string? error { get; init; }
    }

    private record AuthRevokeResponse : Response
    {
        public bool revoked { get; init; }
    }

    private record OAuthAccessResponse : Response
    {
        public string access_token { get; init; } = null!;
        public string scope { get; init; } = null!;
        public string user_id { get; init; } = null!;
        public string team_id { get; init; } = null!;
        public string team_name { get; init; } = null!;
        public IncomingWebHook? incoming_webhook { get; init; }

        public record IncomingWebHook
        {
            public string channel { get; init; } = null!;
            public string channel_id { get; init; } = null!;
            public string configuration_url { get; init; } = null!;
            public string url { get; init; } = null!;
        }
    }
}
