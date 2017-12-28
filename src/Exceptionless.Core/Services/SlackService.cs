using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Queues.Models;
using Foundatio.Queues;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services {
    public class SlackService {
        private readonly HttpClient _client = new HttpClient();
        private readonly IQueue<WebHookNotification> _webHookNotificationQueue;
        private readonly FormattingPluginManager _pluginManager;
        private readonly ISerializer _serializer;
        private readonly ILogger _logger;

        public SlackService(IQueue<WebHookNotification> webHookNotificationQueue, FormattingPluginManager pluginManager, ITextSerializer serializer, ILoggerFactory loggerFactory = null) {
            _webHookNotificationQueue = webHookNotificationQueue;
            _pluginManager = pluginManager;
            _serializer = serializer;
            _logger = loggerFactory.CreateLogger<SlackService>();
        }

        public async Task<SlackToken> GetAccessTokenAsync(string code) {
            if (String.IsNullOrEmpty(code))
                throw new ArgumentNullException(nameof(code));

            var data = new Dictionary<string, string> {
                { "client_id", Settings.Current.SlackAppId },
                { "client_secret", Settings.Current.SlackAppSecret },
                { "code", code },
                { "redirect_uri", new Uri(Settings.Current.BaseURL).GetLeftPart(UriPartial.Authority) }
            };

            string url = $"https://slack.com/api/oauth.access?{data.ToQueryString()}";
            var response = await _client.PostAsync(url).AnyContext();
            var body = await response.Content.ReadAsByteArrayAsync().AnyContext();
            var result = _serializer.Deserialize<OAuthAccessResponse>(body);

            if (!result.ok) {
                _logger.LogWarning("Error getting access token: {Message}, Response: {Response}", result.error ?? result.warning, result);
                return null;
            }

            var token = new SlackToken {
                AccessToken = result.access_token,
                Scopes = result.scope?.Split(new [] {"," }, StringSplitOptions.RemoveEmptyEntries) ?? new string[0],
                UserId = result.user_id,
                TeamId = result.team_id,
                TeamName = result.team_name
            };

            if (result.incoming_webhook != null) {
                token.IncomingWebhook = new SlackToken.IncomingWebHook {
                    Channel = result.incoming_webhook.channel,
                    ChannelId = result.incoming_webhook.channel_id,
                    ConfigurationUrl = result.incoming_webhook.configuration_url,
                    Url = result.incoming_webhook.url
                };
            }

            return token;
        }

        public async Task<bool> RevokeAccessTokenAsync(string token) {
            if (String.IsNullOrEmpty(token))
                throw new ArgumentNullException(nameof(token));

            string url = $"https://slack.com/api/auth.revoke?token={token}";
            var response = await _client.PostAsync(url).AnyContext();
            var body = await response.Content.ReadAsByteArrayAsync().AnyContext();
            var result = _serializer.Deserialize<AuthRevokeResponse>(body);

            if (result.ok && result.revoked || String.Equals(result.error, "invalid_auth"))
                return true;

            _logger.LogWarning("Error revoking token: {Message}, Response: {Response}", result.error ?? result.warning, result);
            return false;
        }

        public Task SendMessageAsync(string organizationId, string projectId, string url, SlackMessage message) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));

            if (String.IsNullOrEmpty(projectId))
                throw new ArgumentNullException(nameof(projectId));

            if (String.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var notification = new WebHookNotification {
                OrganizationId = organizationId,
                ProjectId = projectId,
                Url = url,
                Type = WebHookType.Slack,
                Data = message
            };

            return _webHookNotificationQueue.EnqueueAsync(notification);
        }

        public async Task<bool> SendEventNoticeAsync(PersistentEvent ev, Project project, bool isNew, bool isRegression, int totalOccurrences) {
            var token = project.GetSlackToken();
            if (token?.IncomingWebhook?.Url == null)
                return false;

            bool isCritical = ev.IsCritical();
            var message = _pluginManager.GetSlackEventNotificationMessage(ev, project, isCritical, isNew, isRegression);
            if (message == null) {
                _logger.LogWarning("Unable to create event notification slack message for event {id}.", ev.Id);
                return false;
            }

            await SendMessageAsync(ev.OrganizationId, ev.ProjectId, token.IncomingWebhook.Url, message);
            return true;
        }

        private class Response {
            public bool ok { get; set; }
            public string warning { get; set; }
            public string error { get; set; }
        }

        private class AuthRevokeResponse : Response {
            public bool revoked { get; set; }
        }

        private class OAuthAccessResponse : Response {
            public string access_token { get; set; }
            public string scope { get; set; }
            public string user_id { get; set; }
            public string team_id { get; set; }
            public string team_name { get; set; }
            public IncomingWebHook incoming_webhook { get; set; }

            public class IncomingWebHook {
                public string channel { get; set; }
                public string channel_id { get; set; }
                public string configuration_url { get; set; }
                public string url { get; set; }
            }
        }
    }
}
