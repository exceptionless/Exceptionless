using Exceptionless.Core.Models;
using Exceptionless.Core.Services;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using OAuth2.Client;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Models;

namespace Exceptionless.Web.Security;

public interface IOAuthProviderClient
{
    Task<UserInfo> GetFacebookUserInfoAsync(ExternalAuthInfo authInfo, string appId, string appSecret);
    Task<UserInfo> GetGitHubUserInfoAsync(ExternalAuthInfo authInfo, string appId, string appSecret);
    Task<UserInfo> GetGoogleUserInfoAsync(ExternalAuthInfo authInfo, string appId, string appSecret);
    Task<UserInfo> GetMicrosoftUserInfoAsync(ExternalAuthInfo authInfo, string appId, string appSecret);
    Task<SlackToken?> GetSlackAccessTokenAsync(string code);
}

public sealed class OAuthProviderClient(SlackService slackService) : IOAuthProviderClient
{
    public Task<UserInfo> GetFacebookUserInfoAsync(ExternalAuthInfo authInfo, string appId, string appSecret)
    {
        return GetUserInfoAsync(authInfo, appId, appSecret, (factory, configuration) =>
        {
            configuration.Scope = "email";
            return new FacebookClient(factory, configuration);
        });
    }

    public Task<UserInfo> GetGitHubUserInfoAsync(ExternalAuthInfo authInfo, string appId, string appSecret)
    {
        return GetUserInfoAsync(authInfo, appId, appSecret, (factory, configuration) =>
        {
            configuration.Scope = "user:email";
            return new GitHubClient(factory, configuration);
        });
    }

    public Task<UserInfo> GetGoogleUserInfoAsync(ExternalAuthInfo authInfo, string appId, string appSecret)
    {
        return GetUserInfoAsync(authInfo, appId, appSecret, (factory, configuration) =>
        {
            configuration.Scope = "profile email";
            return new GoogleClient(factory, configuration);
        });
    }

    public Task<UserInfo> GetMicrosoftUserInfoAsync(ExternalAuthInfo authInfo, string appId, string appSecret)
    {
        return GetUserInfoAsync(authInfo, appId, appSecret, (factory, configuration) =>
        {
            configuration.Scope = "wl.emails";
            return new WindowsLiveClient(factory, configuration);
        });
    }

    public Task<SlackToken?> GetSlackAccessTokenAsync(string code)
    {
        return slackService.GetAccessTokenAsync(code);
    }

    private static Task<UserInfo> GetUserInfoAsync<TClient>(
        ExternalAuthInfo authInfo,
        string appId,
        string appSecret,
        Func<IRequestFactory, IClientConfiguration, TClient> createClient
    ) where TClient : OAuth2Client
    {
        var client = createClient(new RequestFactory(), new OAuth2.Configuration.ClientConfiguration
        {
            ClientId = appId,
            ClientSecret = appSecret,
            RedirectUri = authInfo.RedirectUri
        });

        return client.GetUserInfoAsync(authInfo.Code, authInfo.RedirectUri);
    }
}
