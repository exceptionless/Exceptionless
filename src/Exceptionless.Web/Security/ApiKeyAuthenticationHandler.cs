using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Web.Extensions;
using Foundatio.Caching;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Exceptionless.Web.Security;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string ApiKeySchema = "ApiKey";

    public string AuthenticationScheme { get; } = ApiKeySchema;
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public const string BearerScheme = "bearer";
    public const string BasicScheme = "basic";
    public const string TokenScheme = "token";
    private const string OAuthAccessTokenCacheKeyPrefix = "oauth:access:";
    private const string OAuthAccessTokenCacheMiss = "-";

    private readonly ITokenRepository _tokenRepository;
    private readonly IOAuthTokenRepository _oauthTokenRepository;
    private readonly ICacheClient _cacheClient;
    private readonly IUserRepository _userRepository;
    private readonly OAuthService _oauthService;
    private readonly TimeProvider _timeProvider;
    private readonly AppOptions _appOptions;

    public ApiKeyAuthenticationHandler(ITokenRepository tokenRepository, IOAuthTokenRepository oauthTokenRepository, ICacheClient cacheClient, IUserRepository userRepository, OAuthService oauthService, AppOptions appOptions, IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        TimeProvider timeProvider, ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder)
    {
        _tokenRepository = tokenRepository;
        _oauthTokenRepository = oauthTokenRepository;
        _cacheClient = cacheClient;
        _userRepository = userRepository;
        _oauthService = oauthService;
        _appOptions = appOptions;
        _timeProvider = timeProvider;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? authHeaderValue = Request.Headers.TryGetAndReturn("Authorization").FirstOrDefault();
        AuthenticationHeaderValue? authHeader = null;
        if (!String.IsNullOrEmpty(authHeaderValue) && !AuthenticationHeaderValue.TryParse(authHeaderValue, out authHeader))
            return AuthenticateResult.Fail("Unable to parse header");

        string? scheme = authHeader?.Scheme.ToLowerInvariant();
        string? token = null;
        bool isAuthorizationHeaderToken = false;
        if (authHeader is not null && (scheme == BearerScheme || scheme == TokenScheme))
        {
            token = authHeader.Parameter;
            isAuthorizationHeaderToken = true;
        }
        else if (authHeader is not null && scheme == BasicScheme)
        {
            var authInfo = Request.GetBasicAuth();
            if (authInfo is not null)
            {
                if (authInfo.Username.ToLower() == "client")
                    token = authInfo.Password;
                else if (authInfo.Password.ToLower() == "x-oauth-basic" || String.IsNullOrEmpty(authInfo.Password))
                    token = authInfo.Username;
                else
                {
                    User? user;
                    try
                    {
                        user = await _userRepository.GetByEmailAddressAsync(authInfo.Username);
                    }
                    catch (Exception ex)
                    {
                        return AuthenticateResult.Fail(ex);
                    }

                    if (user is not { IsActive: true })
                        return AuthenticateResult.Fail("User is not valid");

                    if (String.IsNullOrEmpty(user.Salt))
                        return AuthenticateResult.Fail("User is not valid");

                    string encodedPassword = authInfo.Password.ToSaltedHash(user.Salt);
                    if (!String.Equals(encodedPassword, user.Password))
                        return AuthenticateResult.Fail("User is not valid");

                    return AuthenticateResult.Success(CreateUserAuthenticationTicket(user));
                }
            }
        }
        else
        {
            token = Request.GetQueryString("access_token");
            if (String.IsNullOrEmpty(token))
                token = Request.GetQueryString("api_key");

            if (String.IsNullOrEmpty(token))
                token = Request.GetQueryString("apikey");
        }

        if (String.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        if (isAuthorizationHeaderToken && scheme == BearerScheme && (IsMcpRequest() || OAuthService.IsOAuthTokenFormat(token)))
            return await AuthenticateOAuthBearerAsync(token);

        var tokenRecord = await GetTokenRecordAsync(token);
        if (tokenRecord is null)
        {
            Logger.LogInformation("Token for {Path} not found", Request.Path);
            return AuthenticateResult.Fail("Token is not valid");
        }

        Request.HttpContext.Items["ApiKey"] = tokenRecord.Id;
        if (tokenRecord.IsDisabled || tokenRecord.IsSuspended)
        {
            if (Request.IsEventPost())
                AppDiagnostics.PostsBlocked.Add(1);

            Logger.LogInformation("Token is disabled or account is suspended for {Path}", Request.Path);
            return AuthenticateResult.Fail("Token is not valid");
        }

        if (tokenRecord.ExpiresUtc.HasValue && tokenRecord.ExpiresUtc.Value < _timeProvider.GetUtcNow().UtcDateTime)
        {
            Logger.LogInformation("Token for {Path} expired on {TokenExpiresUtc}", Request.Path, tokenRecord.ExpiresUtc.Value);
            return AuthenticateResult.Fail("Token is not valid");
        }

        if (!String.IsNullOrEmpty(tokenRecord.UserId))
        {
            var user = await _userRepository.GetByIdAsync(tokenRecord.UserId, o => o.Cache());
            if (user is null)
            {
                Logger.LogInformation("Could not find user {UserId} for token on {Path}", tokenRecord.UserId, Request.Path);
                return AuthenticateResult.Fail("Token is not valid");
            }

            return AuthenticateResult.Success(CreateUserAuthenticationTicket(user, tokenRecord));
        }

        return AuthenticateResult.Success(CreateTokenAuthenticationTicket(tokenRecord));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        await base.HandleChallengeAsync(properties);

        if (!TryGetOAuthResourceForRequest(out var resourceDefinition, out _))
            return;

        Response.Headers.WWWAuthenticate = $"Bearer resource_metadata=\"{GetCanonicalOrigin()}/.well-known/oauth-protected-resource{resourceDefinition.Path}\"";
    }

    private async Task<AuthenticateResult> AuthenticateOAuthBearerAsync(string token)
    {
        if (!OAuthService.IsOAuthTokenFormat(token))
            return AuthenticateResult.Fail("Token is not valid");

        var tokenRecord = await GetOAuthTokenRecordAsync(token);
        if (tokenRecord is null)
        {
            Logger.LogInformation("OAuth token for {Path} not found", Request.Path);
            return AuthenticateResult.Fail("Token is not valid");
        }

        Request.HttpContext.Items["ApiKey"] = tokenRecord.Id;
        if (tokenRecord.IsDisabled || tokenRecord.IsSuspended)
        {
            Logger.LogInformation("OAuth token is disabled or account is suspended for {Path}", Request.Path);
            return AuthenticateResult.Fail("Token is not valid");
        }

        if (tokenRecord.ExpiresUtc.HasValue && tokenRecord.ExpiresUtc.Value < _timeProvider.GetUtcNow().UtcDateTime)
        {
            Logger.LogInformation("OAuth token for {Path} expired on {TokenExpiresUtc}", Request.Path, tokenRecord.ExpiresUtc.Value);
            return AuthenticateResult.Fail("Token is not valid");
        }

        if (!IsOAuthResourceValid(tokenRecord.Resource))
            return AuthenticateResult.Fail("Token resource is not valid");

        if (tokenRecord.OrganizationIds.Count == 0)
            return AuthenticateResult.Fail("Token organization access is not valid");

        if (!await _oauthService.IsAccessTokenClientValidAsync(tokenRecord.ClientId))
            return AuthenticateResult.Fail("OAuth client is not valid");

        var user = await _userRepository.GetByIdAsync(tokenRecord.UserId, o => o.Cache());
        if (user is null)
        {
            Logger.LogInformation("Could not find user {UserId} for OAuth token on {Path}", tokenRecord.UserId, Request.Path);
            return AuthenticateResult.Fail("Token is not valid");
        }

        if (!user.IsActive)
        {
            await DisableOAuthTokenAsync(tokenRecord);
            return AuthenticateResult.Fail("User is not valid");
        }

        var activeOAuthOrganizationIds = user.GetActiveOAuthOrganizationIds(tokenRecord);
        if (activeOAuthOrganizationIds.Count == 0)
        {
            await DisableOAuthTokenAsync(tokenRecord);
            return AuthenticateResult.Fail("Token organization access is not valid");
        }

        return AuthenticateResult.Success(CreateOAuthUserAuthenticationTicket(user, tokenRecord, activeOAuthOrganizationIds));
    }

    private Task<Token?> GetTokenRecordAsync(string token)
    {
        return _tokenRepository.GetByIdAsync(token, o => o.Cache());
    }

    private async Task<OAuthToken?> GetOAuthTokenRecordAsync(string token)
    {
        string accessTokenHash = OAuthService.CreateTokenHash(token);
        string cacheKey = GetOAuthAccessTokenCacheKey(accessTokenHash);
        string? tokenId = await _cacheClient.GetAsync<string?>(cacheKey, null);
        if (String.Equals(tokenId, OAuthAccessTokenCacheMiss, StringComparison.Ordinal))
            return null;

        if (!String.IsNullOrEmpty(tokenId))
        {
            var cachedToken = await _oauthTokenRepository.GetByIdAsync(tokenId, o => o.Cache());
            if (cachedToken is not null && String.Equals(cachedToken.AccessTokenHash, accessTokenHash, StringComparison.Ordinal))
                return cachedToken;

            await _cacheClient.RemoveAsync(cacheKey);
        }

        var results = await _oauthTokenRepository.GetByAccessTokenHashAsync(accessTokenHash);
        var tokenRecord = results.Documents.FirstOrDefault();
        await _cacheClient.SetAsync(cacheKey, tokenRecord?.Id ?? OAuthAccessTokenCacheMiss, TimeSpan.FromMinutes(5));

        return tokenRecord;
    }

    private static string GetOAuthAccessTokenCacheKey(string accessTokenHash) => OAuthAccessTokenCacheKeyPrefix + accessTokenHash;

    private Task DisableOAuthTokenAsync(OAuthToken token)
    {
        token.IsDisabled = true;
        token.RefreshTokenHash = null;
        token.UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime;
        return _oauthTokenRepository.SaveAsync(token, o => o.ImmediateConsistency());
    }

    private AuthenticationTicket CreateUserAuthenticationTicket(User user, Token? token = null)
    {
        Request.SetUser(user);

        var principal = new ClaimsPrincipal(user.ToIdentity(token));
        return new AuthenticationTicket(principal, CreateAuthenticationProperties(token), Options.AuthenticationScheme);
    }

    private AuthenticationTicket CreateOAuthUserAuthenticationTicket(User user, OAuthToken token, IReadOnlyCollection<string> organizationIds)
    {
        Request.SetUser(user);

        var principal = new ClaimsPrincipal(user.ToIdentity(token, organizationIds));
        return new AuthenticationTicket(principal, CreateAuthenticationProperties(token), Options.AuthenticationScheme);
    }

    private AuthenticationTicket CreateTokenAuthenticationTicket(Token token)
    {
        var principal = new ClaimsPrincipal(token.ToIdentity());
        return new AuthenticationTicket(principal, CreateAuthenticationProperties(token), Options.AuthenticationScheme);
    }

    private AuthenticationProperties CreateAuthenticationProperties(Token? token)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        return new AuthenticationProperties
        {
            ExpiresUtc = token?.ExpiresUtc ?? utcNow.AddHours(12),
            IssuedUtc = token?.CreatedUtc ?? utcNow
        };
    }

    private AuthenticationProperties CreateAuthenticationProperties(OAuthToken token)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        return new AuthenticationProperties
        {
            ExpiresUtc = token.ExpiresUtc ?? utcNow.AddHours(12),
            IssuedUtc = token.CreatedUtc
        };
    }

    private bool IsOAuthResourceValid(string? resource)
    {
        if (!TryGetOAuthResourceForRequest(out _, out string expectedResource))
            return false;

        return OAuthService.IsExpectedResource(resource, expectedResource);
    }

    private bool TryGetOAuthResourceForRequest(out OAuthResourceDefinition resourceDefinition, out string expectedResource)
    {
        foreach (var candidate in OAuthService.ProtectedResources)
        {
            if (!Request.Path.StartsWithSegments(new PathString(candidate.Path), StringComparison.OrdinalIgnoreCase))
                continue;

            resourceDefinition = candidate;
            expectedResource = OAuthService.CreateResourceUri(GetCanonicalOrigin(), candidate);
            return true;
        }

        resourceDefinition = null!;
        expectedResource = String.Empty;
        return false;
    }

    private bool IsMcpRequest()
    {
        return Request.Path.StartsWithSegments(new PathString(OAuthService.McpResource.Path), StringComparison.OrdinalIgnoreCase);
    }

    private string GetCanonicalOrigin()
    {
        return new Uri(_appOptions.BaseURL).GetLeftPart(UriPartial.Authority);
    }
}

public static class ApiKeyAuthMiddlewareExtensions
{
    public static AuthenticationBuilder AddApiKeyAuthentication(this AuthenticationBuilder builder)
    {
        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.ApiKeySchema, null, _ => { });
    }
}
