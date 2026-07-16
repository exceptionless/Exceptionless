using Exceptionless.Core.Models;
using Exceptionless.Web.Models;
using Exceptionless.Web.Security;
using OAuth2.Models;

namespace Exceptionless.Tests.Utility;

public sealed class TestOAuthProviderClient : IOAuthProviderClient
{
    public Task<UserInfo> GetFacebookUserInfoAsync(ExternalAuthInfo authInfo, string appId, string appSecret)
    {
        return GetUserInfoAsync("Facebook", authInfo);
    }

    public Task<UserInfo> GetGitHubUserInfoAsync(ExternalAuthInfo authInfo, string appId, string appSecret)
    {
        return GetUserInfoAsync("GitHub", authInfo);
    }

    public Task<UserInfo> GetGoogleUserInfoAsync(ExternalAuthInfo authInfo, string appId, string appSecret)
    {
        return GetUserInfoAsync("Google", authInfo);
    }

    public Task<UserInfo> GetMicrosoftUserInfoAsync(ExternalAuthInfo authInfo, string appId, string appSecret)
    {
        return GetUserInfoAsync("WindowsLive", authInfo);
    }

    public Task<SlackToken?> GetSlackAccessTokenAsync(string code)
    {
        return Task.FromResult<SlackToken?>(new SlackToken
        {
            AccessToken = $"xoxp-{code}",
            Scopes = ["incoming-webhook"],
            TeamId = $"team-{code}",
            TeamName = "Test Team",
            UserId = $"user-{code}",
            IncomingWebhook = new SlackToken.IncomingWebHook
            {
                Channel = "#general",
                ChannelId = $"channel-{code}",
                ConfigurationUrl = $"https://slack.test/config/{code}",
                Url = $"https://hooks.slack.test/{code}"
            }
        });
    }

    public static string GetEmailAddress(string providerUserId)
    {
        return $"{providerUserId}@exceptionless.test";
    }

    private static Task<UserInfo> GetUserInfoAsync(string providerName, ExternalAuthInfo authInfo)
    {
        return Task.FromResult(new UserInfo
        {
            Id = authInfo.Code,
            ProviderName = providerName,
            Email = GetEmailAddress(authInfo.Code),
            FirstName = providerName,
            LastName = "User"
        });
    }
}
