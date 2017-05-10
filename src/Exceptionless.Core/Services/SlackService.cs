using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Exceptions;
using Foundatio.Logging;
using Foundatio.Serializer;

namespace Exceptionless.Core.Services {
    public class SlackService {
        private readonly HttpClient _client = new HttpClient();
        private readonly ISerializer _serializer;
        private readonly ILogger _logger;

        public SlackService(ISerializer serializer, ILoggerFactory loggerFactory = null) {
            _serializer = serializer;
            _logger = loggerFactory.CreateLogger<SlackService>();
        }

        public async Task<SlackToken> GetAccessTokenAsync(string code) {
            if (String.IsNullOrEmpty(code))
                throw new ArgumentNullException(nameof(code));

            var data = await _serializer.SerializeToStringAsync(new Dictionary<string, string> {
                { "client_id", Settings.Current.SlackAppId },
                { "client_secret", Settings.Current.SlackAppSecret },
                { "code", code }
            }).AnyContext();

            var response = await _client.PostAsync("https://slack.com/api/oauth.access", new StringContent(data, Encoding.UTF8, "application/json")).AnyContext();
            var body = await response.Content.ReadAsByteArrayAsync().AnyContext();
            var result = await _serializer.DeserializeAsync<OAuthAccessResponse>(body).AnyContext();

            if (!result.ok) {
                _logger.Warn().Message("Error getting access token: {0}", result.error ?? result.warning).Property("Response", result).Write();
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
                    Channel = token.IncomingWebhook.Channel,
                    ChannelId = token.IncomingWebhook.ChannelId,
                    ConfigurationUrl = token.IncomingWebhook.ConfigurationUrl,
                    Url = token.IncomingWebhook.Url
                };
            }

            return token;
        }

        public async Task<bool> RevokeAccessTokenAsync(string token) {
            if (String.IsNullOrEmpty(token))
                throw new ArgumentNullException(nameof(token));

            var data = await _serializer.SerializeToStringAsync(new Dictionary<string, string> {
                { "token", token }
            }).AnyContext();

            var response = await _client.PostAsync("https://slack.com/api/auth.revoke", new StringContent(data, Encoding.UTF8, "application/json")).AnyContext();
            var body = await response.Content.ReadAsByteArrayAsync().AnyContext();
            var result = await _serializer.DeserializeAsync<AuthRevokeResponse>(body).AnyContext();

            if (result.ok && result.revoked || String.Equals(result.error, "invalid_auth"))
                return true;

            _logger.Warn().Message("Error revoking token: {0}", result.error ?? result.warning).Property("Response", result).Write();
            return false;
        }

        public async Task SendMessageAsync(string url, string message) {
            if (String.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));

            if (String.IsNullOrEmpty(message))
                throw new ArgumentNullException(nameof(message));

            var data = await _serializer.SerializeToStringAsync(new Dictionary<string, string> {
                { "text", message }
            }).AnyContext();

            var response = await _client.PostAsync(url, new StringContent(data, Encoding.UTF8, "application/json")).AnyContext();
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsByteArrayAsync().AnyContext();
            var result = await _serializer.DeserializeAsync<Response>(body).AnyContext();

            _logger.Warn().Message("Error sending message: [{0}] {1}", response.StatusCode, result.error ?? result.warning).Property("Response", result).Write();
            if ((int)response.StatusCode == 429 && response.Headers.RetryAfter.Date.HasValue)
                throw new RateLimitException { RetryAfter = response.Headers.RetryAfter.Date.Value.UtcDateTime };

            throw new WebHookException(result.error ?? result.warning) {
                StatusCode = (int)response.StatusCode,
                Unauthorized = response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone
            };
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
